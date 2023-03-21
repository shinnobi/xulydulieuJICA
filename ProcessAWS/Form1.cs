using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

using System.Configuration;

using System.IO;
using System.Linq;
using System.Reflection;

using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

using System.Windows.Forms;
using LeanWork.IO.FileSystem;
using Newtonsoft.Json;
using MongoDB.Driver;
using MongoDB.Bson;
using System.Globalization;
using System.Timers;

namespace ProcessAWS
{
    public partial class ProcessTp1 : Form
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public string _sourceJica;

        ConcurrentQueue<string> Queue = new ConcurrentQueue<string>();
        private bool _isError = false;
        private DateTime _beginError = new DateTime();
        public Dictionary<string, int> Config = new Dictionary<string, int>();
        public string ConfigFile;
        public string Total10MJsonFile;
        public string Total1HJsonFile;
        static MongoClient _dbClient;
        TimeZoneInfo VietNamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        //private Dictionary<string, string> DaiKhuVuc;
        private static IMongoDatabase database;
        private IMongoCollection<BsonDocument> collection10m;
        private IMongoCollection<BsonDocument> collection1h;
        private IMongoCollection<BsonDocument> collection1day;
        private IMongoCollection<BsonDocument> collectionNews;

        public string Total1HJsonFolder = " ";
        public string Total10MJsonFolder = " ";
        public ProcessTp1()
        {
            InitializeComponent();
            MongoSetup();

            //    var sourceDongBac = ConfigurationManager.AppSettings["SourceDongBac"];
            //   var sourceBtb = ConfigurationManager.AppSettings["SourceBTB"];
            //   var sourceVb = ConfigurationManager.AppSettings["SourceVB"];
            //    var sourceTb = ConfigurationManager.AppSettings["SourceTB"];
            //    var sourceDongBang = ConfigurationManager.AppSettings["SourceDongBang"];
            //  DaiKhuVuc = new Dictionary<string, string>()
            //{
            //    {"NorthWest",sourceTb},
            //    {"VietBac",sourceVb },
            //    {"Northen Delta",sourceDongBang },
            //    {"NorthCentral",sourceBtb },
            //    {"NorthEast",sourceDongBac}
            //};

        }
        private void MongoSetup()
        {
            //const string username = "ai";
            //const string password = "ai@0258";
            //const string mongoDbAuthMechanism = "SCRAM-SHA-1";
            //MongoInternalIdentity internalIdentity =
            //          new MongoInternalIdentity("admin", username);
            //PasswordEvidence passwordEvidence = new PasswordEvidence(password);
            //MongoCredential mongoCredential = new MongoCredential(mongoDbAuthMechanism, internalIdentity, passwordEvidence);
            //List<MongoCredential> credentials = new List<MongoCredential>() { mongoCredential };

            //MongoClientSettings settings = new MongoClientSettings {Credentials = credentials};
            //// comment this line below if your mongo doesn't run on secured mode
            //const string mongoHost = "192.168.1.71";
            //const int port = 14680;
            //MongoServerAddress address = new MongoServerAddress(mongoHost, port);
            //settings.Server = address;

            //_dbClient = new MongoClient(settings);
            string executableLocation = Path.GetDirectoryName(
             Assembly.GetExecutingAssembly().Location);
            string pemLocation = Path.Combine(executableLocation, "weathervietnam.vn.pem");
            string srtLocation = Path.Combine(executableLocation, "weathervietnam.vn.crt");
            //ssl setup
            //string connectionString =
            //    @"mongodb://mongo4.weathervietnam.vn:60004,mongo5.weathervietnam.vn:60005,mongo6.weathervietnam.vn:60006,mongo7.weathervietnam.vn:60007/?replicaSet=replicaAws&ssl=true&tlsAllowInvalidCertificates=true&ssl_ca_certs=" +
            //   srtLocation+ "&ssl_certfile=" + pemLocation;
            string connectionString = "mongodb://admin:cnTT%400258@tckttvmongo4.weathervietnam.vn:62004,tckttvmongo5.weathervietnam.vn:62005,tckttvmongo6.weathervietnam.vn:62006,tckttvmongo7.weathervietnam.vn:62007/?replicaSet=replicaTCkttv&authSource=admin";

            MongoClientSettings settings = MongoClientSettings.FromUrl(new MongoUrl(connectionString));

            //Disable certificate verification, if it is not issued for you
            //settings.VerifySslCertificate = false;
            _dbClient = new MongoClient(settings);

            database = _dbClient.GetDatabase("jica-backend");
            collection10m = database.GetCollection<BsonDocument>("rain10m");
            collection1h = database.GetCollection<BsonDocument>("rain1h");
            collection1day = database.GetCollection<BsonDocument>("rain1day");
            collectionNews = database.GetCollection<BsonDocument>("news");

        }
        private void ProcessTP1_Load(object sender, EventArgs e)
        {
            _sourceJica = ConfigurationManager.AppSettings["SourceJICA"];
            ConfigFile = ConfigurationManager.AppSettings["ConfigFile"];
            Total1HJsonFolder = ConfigurationManager.AppSettings["rainToTal1hFolder"];
            Total10MJsonFolder = ConfigurationManager.AppSettings["rainToTal10mFolder"];

            //luc khoi dong thi lay lai file cu trong vong 2 tieng
            Thread t = new Thread(() => ProcessErrorWatcher(_sourceJica,".dat")) { IsBackground = true };
            t.Start();
            UpdateNews();
            System.Timers.Timer timer =new System.Timers.Timer();
            timer.Interval = 5*60*1000;
            timer.Enabled = true;
            timer.Elapsed += timerGetNews_Tick;
            timer.Start();

            //start watcher

            RunRecoveringWatcher(_sourceJica, ".dat");



            //// xu ly queue
            Thread processQueue = new Thread(DeQueue) { IsBackground = true };
            processQueue.Start();

            //connect mongo lay danh sach station cho vao list
        }
        public void timerGetNews_Tick(object sender, EventArgs e)
        {
            UpdateNews();
        }
        public void RunRecoveringWatcher(string sourceFolder, string filter)
        {

            var watcher = new RecoveringFileSystemWatcher(sourceFolder);

            watcher.IncludeSubdirectories = true;
            watcher.NotifyFilter = NotifyFilters.FileName
                                           | NotifyFilters.Size;
            watcher.Filter = "*" + filter;
            watcher.All += (_, e) =>
            {
                if (_isError == true)
                {
                    _isError = false;
                    new Thread(() =>
                    {
                        ProcessErrorWatcher(sourceFolder, filter);
                    }).Start();

                }
                if (e.ChangeType == WatcherChangeTypes.Changed || e.ChangeType == WatcherChangeTypes.Renamed || e.ChangeType == WatcherChangeTypes.Created)
                {
                    //new Thread(() =>
                    //{
                    OnChange(e.FullPath);
                    //}).Start();


                }
            };

            watcher.Error += (_, e) =>
            {
                if (_isError == false)
                {
                    _beginError = DateTime.Now;
                    _isError = true;
                }
                BeginInvoke((MethodInvoker)delegate
                {

                });

            };
            watcher.EnableRaisingEvents = true;
            watcher.OrderByOldestFirst = false;
        }

        public void OnChange(string path)
        {
            if (!Queue.Contains(path))
            {
                Queue.Enqueue(path);
            }

        }
        //showlog
        public void ShowLog(string data)
        {
            if (rtb_Log.Lines.Count() > 1000)
                rtb_Log.Clear();

            rtb_Log.AppendText(data);
        }

        public void ProcessErrorWatcher(string sourceFolder, string filter)
        {
            Invoke((MethodInvoker)delegate
            {
                ShowLog(String.Format("Xử lý file cũ...\n"));
            });
            var lstMissingFiles = GetMissingFiles(sourceFolder, filter);
            foreach (var file in lstMissingFiles)
            {

                if (!Queue.Contains(file.FullName) && file.Extension == filter)
                {

                    Queue.Enqueue(file.FullName);
                }
            }
        }
        //ham get file bi thieu luc network down

        public FileInfo[] GetMissingFiles(string folder, string filter)
        {
            //var lstFileInfos = new List<FileInfo>();
            DirectoryInfo dirInfo = new DirectoryInfo(folder);
            FileInfo[] lstFiles = dirInfo.EnumerateFiles("*" + filter, SearchOption.AllDirectories)
            .AsParallel()
            .ToArray();
            return lstFiles;
        }

        public void DeQueue()
        {
            while (true)
            {
                if (Queue.Count != 0)
                {
                    string fn;
                    Queue.TryDequeue(out fn);
                    if (!IsFileLocked(fn))
                    {
                        ProcessOne(fn);
                    }
                    else
                    {
                        Queue.Enqueue(fn);
                    }
                }
                else
                {
                    Thread.Sleep(1000);
                }
            }
        }

        //Thread Write2Rain10m;
        //Thread Write2RObs;
        //Thread Write2Rain19h;
        //Thread Write2Rain24h;


        // ham xu ly 1 file
        public void ProcessOne(string path)
        {
            if (!path.Contains(_sourceJica))
            {
                ProcessAwsTp1(path);
            }
            else
            {
                ProcessJica(path);
            }
        }

        public Dictionary<string, float> CalRainToTal(string jsonFile)
        {
            //filename =  20210524.json
            if (File.Exists(jsonFile))
            {
                string jsonData = File.ReadAllText(jsonFile);
                var desirializedData = JsonConvert.DeserializeObject<Dictionary<string, float>>(jsonData);
                return desirializedData;
            }
            using (File.Create(jsonFile))
            {
                return new Dictionary<string, float>();
            }
        }

        public void WriteRainToTalFile(string jsonFile, Dictionary<string, float> jsonData)
        {
            string strJson = JsonConvert.SerializeObject(jsonData);
            using (var fs = TryCreateFileStream(jsonFile, FileMode.OpenOrCreate, FileAccess.ReadWrite,
                               FileShare.None))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(strJson);
                fs.Write(bytes, 0, bytes.Length);

            }

        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        public void ProcessJica(string path)
        {

            var filename = Path.GetFileName(path);
            if (!filename.Contains("vietnam_st"))
            {
                return;
            };
            var currentLine = 0;
            if (Config.ContainsKey(filename))
            {
                currentLine = Config[filename];
            }
            else
            {
                if (new FileInfo(ConfigFile).Length != 0)
                {
                    string jsonData = File.ReadAllText(ConfigFile);
                    Config = JsonConvert.DeserializeObject<Dictionary<string, int>>(jsonData);
                    if (Config.ContainsKey(filename))
                    {
                        currentLine = Config[filename];
                    }
                }
                else
                {
                    Config[filename] = 0;
                    var jsonData = JsonConvert.SerializeObject(Config, Formatting.Indented);
                    using (var fs = TryCreateFileStream(ConfigFile, FileMode.OpenOrCreate, FileAccess.ReadWrite,
                                   FileShare.None))
                    {
                        byte[] bytes = Encoding.UTF8.GetBytes(jsonData);
                        fs.Write(bytes, 0, bytes.Length);

                    }

                }
            }
            if (path.Contains("TBL110.dat"))
            {
                var lines = File.ReadAllLines(path);
                lines = lines.Skip(currentLine).ToArray();

                foreach (var line in lines)
                {

                    try
                    {
                        if (string.IsNullOrEmpty(line))
                        {
                            return;
                        }
                        var listData = line.Split(',');
                        var obj = new JSON();
                        obj.StationNo = listData[0];


                        DateTime dt = DateTime.ParseExact(listData[5] + listData[4] + listData[3] + " " + listData[2],
                            "yyyyMMdd HH.mm.ss", null);
                        //var dt = new DateTime(listData[5],listData[4], 15, 16, 10, 00);
                        var utc = TimeZoneInfo.ConvertTimeToUtc(dt, VietNamTimeZone);

                        obj.CreateTime = DateTime.UtcNow;
                        obj.UpdateTime = DateTime.UtcNow;
                        obj.DataTime = utc;
                        obj.Value = float.Parse(listData[10], CultureInfo.InvariantCulture.NumberFormat);
                        var strDateTimeUtcFile = Total10MJsonFolder + "\\" + utc.ToString("yyyyMMdd") + ".json";
                        var datetimeRainTotal = CalRainToTal(strDateTimeUtcFile);

                        //check stationid co ton tai khong thi tao moi va gan gia tri total rain

                        if (!datetimeRainTotal.ContainsKey(obj.StationNo))
                        {

                            datetimeRainTotal[obj.StationNo] = 0f;
                        }
                        var stationValue = datetimeRainTotal[obj.StationNo];
                        obj.RainTotal = obj.Value + stationValue;
                        InsertToMongo(obj, collection10m);
                        datetimeRainTotal[obj.StationNo] = obj.RainTotal;
                        currentLine++;
                        WriteRainToTalFile(strDateTimeUtcFile, datetimeRainTotal);

                    }
                    catch (Exception ex)
                    {
                        Invoke((MethodInvoker)delegate
                        {
                            ShowLog(String.Format(ex.GetType().ToString()));
                            ShowLog(String.Format("Không đọc được file: " + path + "\n"));
                        });

                        log.Debug("file " + path + " không đúng định dạng " + path + "__" + line + "\n");
                        return;
                    }



                }
                Config[filename] = currentLine;
                var jsonData = JsonConvert.SerializeObject(Config, Formatting.Indented);
                using (var fs = TryCreateFileStream(ConfigFile, FileMode.OpenOrCreate, FileAccess.ReadWrite,
                               FileShare.None))
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(jsonData);
                    fs.Write(bytes, 0, bytes.Length);

                }
                Invoke((MethodInvoker)delegate
                {
                    ShowLog(String.Format("Đã xử lý xong file: " + path + "\n"));
                });



            }
            if (path.Contains("TBL160.dat"))
            {
                var lines = File.ReadAllLines(path);
                lines = lines.Skip(currentLine).ToArray();

                foreach (var line in lines)
                {
                    try
                    {
                        if (string.IsNullOrEmpty(line))
                        {
                            return;
                        }
                        var listData = line.Split(',');
                        var obj = new JSON();
                        obj.StationNo = listData[0];
                        DateTime dt = DateTime.ParseExact(listData[5] + listData[4] + listData[3] + " " + listData[2],
                            "yyyyMMdd HH.mm.ss", null);
                        //var dt = new DateTime(listData[5],listData[4], 15, 16, 10, 00);
                        var utc = TimeZoneInfo.ConvertTimeToUtc(dt, VietNamTimeZone);

                        obj.DataTime = utc;
                        obj.CreateTime = DateTime.UtcNow;
                        obj.UpdateTime = DateTime.UtcNow;
                        obj.Value = float.Parse(listData[10], CultureInfo.InvariantCulture.NumberFormat);
                        var strDateTimeUtcFile = Total1HJsonFolder + "\\" + utc.ToString("yyyyMMdd") + ".json";
                        var datetimeRainTotal = CalRainToTal(strDateTimeUtcFile);

                        //check stationid co ton tai khong thi tao moi va gan gia tri total rain

                        if (!datetimeRainTotal.ContainsKey(obj.StationNo))
                        {

                            datetimeRainTotal[obj.StationNo] = 0f;
                        }
                        var stationValue = datetimeRainTotal[obj.StationNo];
                        obj.RainTotal = obj.Value + stationValue;
                        InsertToMongo(obj, collection1h);
                        datetimeRainTotal[obj.StationNo] = obj.RainTotal;
                        WriteRainToTalFile(strDateTimeUtcFile, datetimeRainTotal);
                        UpdateRain(collection1day, utc, obj.Value, obj.StationNo, true);
                        currentLine++;
                    }
                    catch (Exception ex)
                    {
                        Invoke((MethodInvoker)delegate
                        {
                            ShowLog(String.Format(ex.GetType().ToString()));
                            ShowLog(String.Format("Không đọc được file: " + path + "\n"));
                        });

                        log.Debug("file " + path + " không đúng định dạng " + path + "__" + line + "\n");
                        return;
                    }
                }
                Config[filename] = currentLine;
                var jsonData = JsonConvert.SerializeObject(Config, Formatting.Indented);
                using (var fs = TryCreateFileStream(ConfigFile, FileMode.OpenOrCreate, FileAccess.ReadWrite,
                               FileShare.None))
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(jsonData);
                    fs.Write(bytes, 0, bytes.Length);

                }
                Invoke((MethodInvoker)delegate
                {
                    ShowLog(String.Format("Đã xử lý xong file: " + path + "\n"));
                });



            }
        }

        public void UpdateRain(IMongoCollection<BsonDocument> rain1Day, DateTime utc, float value, string stationId, bool day)
        {
            try
            {

                var date = day ? new DateTime(utc.Year, utc.Month, utc.Day, 0, 0, 0, DateTimeKind.Utc) : new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, DateTimeKind.Utc);
                var filter = Builders<BsonDocument>.Filter.Eq("DateTime", date) &
                             Builders<BsonDocument>.Filter.Eq("StationID", stationId);
                var data = rain1Day.Find(filter).FirstOrDefault();
                if (data != null)
                {
                    var currentRainDay = data["Value"].AsDouble;
                    currentRainDay = currentRainDay + value;
                    var update = Builders<BsonDocument>.Update.Set("Value", currentRainDay).Set("UpdatedTime", DateTime.UtcNow);
                    rain1Day.UpdateOne(filter, update);
                }
                else
                {
                    var doc = new BsonDocument
            {
                {"StationID",stationId },
                {"DateTime",  date},
                {"CreatedTime",DateTime.UtcNow},
                {"UpdatedTime",DateTime.UtcNow},
                {"Value",value},
            };
                    rain1Day.InsertOne(doc);
                }

            }
            catch (Exception ex)
            {

                Invoke((MethodInvoker)delegate
                {
                    ShowLog(String.Format(ex.GetType().ToString()));
                    ShowLog(String.Format("Không insert được vào mongodb rain1day\n"));
                    log.Debug("Không insert được vào mongodb rain1day \n" + ex.Message);
                });
            }

        }
        public void InsertToMongo(JSON obj, IMongoCollection<BsonDocument> collection)
        {

            var doc = new BsonDocument
            {
                {"StationID", obj.StationNo},
                {"DateTime", obj.DataTime},
                {"CreatedTime",obj.CreateTime},
                {"UpdatedTime",obj.UpdateTime},
                {"RainTotal",obj.RainTotal},
                {"Value", obj.Value},
            };
            try
            {
                collection.InsertOne(doc);
            }
            catch (Exception ex)
            {
                Invoke((MethodInvoker)delegate
                {
                    ShowLog(String.Format(ex.GetType().ToString()));
                    ShowLog(String.Format("Không insert được vào mongodb \n"));
                    log.Debug("Không insert được vào mongodb \n" + ex.Message);
                });


            }
        }

        public void ProcessAwsTp1(string path)
        {
            string stationNo;
            DateTime datetimeBegin;
            float value;
            float rainObs;



            try
            {
                string myString = System.IO.File.ReadAllText(path);
                myString = Regex.Replace(myString.Replace(System.Environment.NewLine, " "), " {2,}", " ");
                String[] data = myString.Split(' ');
                if (CheckIfNull(path, data[0])) { return; };
                stationNo = data[0];
                var obs = float.Parse(data[3], CultureInfo.InvariantCulture.NumberFormat);
                DateTime datetimeEnd;
                if (data[2].Substring(8, 2) == "24")
                {

                    datetimeEnd = DateTime.ParseExact(data[2].Remove(8, 2).Insert(8, "00"), "yyyyMMddHHmmss", null).AddDays(1);
                }
                else
                {
                    datetimeEnd = DateTime.ParseExact(data[2], "yyyyMMddHHmmss", null);
                }

                datetimeBegin = datetimeEnd.AddMinutes(obs * -1);
                //obj.PrjID = 7;
                rainObs = float.Parse(data[6], CultureInfo.InvariantCulture.NumberFormat);
                if (CheckIfNull(path, data[4])) { return; };

                try
                {
                    var sumRain10M = 0f;
                    // var lstRain10m = Deserialize(rain10minJsonFile);
                    for (int i = 1; i <= obs / 10; i++)
                    {
                        if (CheckIfNull(path, data[i + 7])) { return; }
                        value = float.Parse(data[i + 7], CultureInfo.InvariantCulture.NumberFormat) / 10;

                        var obj10M = CreateObj(stationNo, datetimeBegin.AddMinutes(10 * i), value);
                        InsertToMongo(obj10M, collection10m);
                        sumRain10M = sumRain10M + value;
                    }
                    if (Math.Abs(sumRain10M - rainObs) > 0.00001)
                    {
                        if (Math.Abs(sumRain10M - rainObs / 10) < 0.00001)
                        {
                            rainObs = rainObs / 10;
                        }
                        else
                        {

                            return;
                        }

                    }
                    if (Math.Abs(obs - 60) < 0.0001)
                    {
                        var obj1H = CreateObj(stationNo, datetimeEnd, rainObs);
                        InsertToMongo(obj1H, collection1h);
                    }
                    else
                    {
                        UpdateRain(collection1h, datetimeEnd, rainObs, stationNo, false);
                    }

                    UpdateRain(collection1day, datetimeEnd, rainObs, stationNo, true);


                }

                catch (Exception ex)
                {
                    Invoke((MethodInvoker)delegate
                    {
                        ShowLog(String.Format("Không đọc được file: " + path + "\n"));
                        log.Debug("file " + path + " không đúng định dạng " + ex.Message + "\n");
                    });
                    return;
                }

            }

            catch (Exception ex)
            {
                Invoke((MethodInvoker)delegate
                {
                    ShowLog(String.Format("Không đọc được file: " + path + "\n"));
                    log.Debug("file " + path + " không đúng định dạng " + ex.Message);
                });
                return;
            }
            Invoke((MethodInvoker)delegate
            {
                ShowLog(String.Format("Đã xử lý xong file: " + path + "\n"));
            });
        }




        public JSON CreateObj(string stationNo, DateTime datatime, float value)
        {
            var obj = new JSON();
            obj.StationNo = stationNo;


            var utc = TimeZoneInfo.ConvertTimeToUtc(datatime, VietNamTimeZone);
            obj.Value = value;
            obj.DataTime = utc;
            obj.CreateTime = DateTime.UtcNow;
            obj.UpdateTime = DateTime.UtcNow;
            return obj;
        }

        public bool CheckIfNull(string path, string txt)
        {
            if (string.IsNullOrEmpty(txt))
            {
                log.Debug("file " + path + " không đúng định dạng");
                return true;
            }
            return false;
        }


        // kiem tra file co bi chiem dung khong
        public bool IsFileLocked(string fn)
        {
            try
            {
                var fileInfo = new FileInfo(fn);
                using (fileInfo.OpenRead())
                {
                    //
                }
            }
            catch
            {
                return true;
            }
            return false;
        }


        private FileStream TryCreateFileStream(string filename, FileMode fileMode, FileAccess fileAccess,
          FileShare fileShare)
        {
            while (true)
            {
                try
                {
                    return new FileStream(filename, fileMode, fileAccess, fileShare);
                }
                catch (IOException)
                {
                    Thread.Sleep(1000);
                }
            }
        }

        //gửi dữ liệu
        private string editContent(string content)
        {
            //	https://thoitietvietnam.gov.vn/
            if (content.Contains("<img") && content.Contains("src"))
            {
                content = content.Replace("src=\"", "src=\"https://thoitietvietnam.gov.vn");
               
            }
            return content;

        }
        public void UpdateNews()
        {
            MobileAppEntities db = new MobileAppEntities();
            var res = from c in db.News where c.StatusID == 6 select new { code = c.NewsID, title = c.Tittle, content = c.Content, created_at = c.Date_Created, updated_at = c.Date_Modify, validated_from = c.Date_Modify, StatusID = c.StatusID, category_name = c.CategoryNew.CategoryNewName, category_code = c.CategoryNewID };
            var news = res.ToList();
            var filterNews = Builders<BsonDocument>.Filter.Eq("status", "active");
            var newsMongo = collectionNews.Find(filterNews).ToList();

            foreach (var tintuc in news)
            {
                try
                {
                    string title = tintuc.title;
                    string content = tintuc.content;
                    content = editContent(content);
                    var filter = Builders<BsonDocument>.Filter.Eq("code", tintuc.code);
                    var data = collectionNews.Find(filter).FirstOrDefault();
                    if (data != null)
                    {
                        

                        var update = Builders<BsonDocument>.Update.Set("status", "active").Set("title", title).Set("content", content);
                        collectionNews.UpdateOne(filter, update);

                        Invoke((MethodInvoker)delegate
                        {

                            ShowLog(String.Format("Cập nhật thành công bản tin\n"));

                        });
                    }
                    else
                    {
                        var doc = new BsonDocument
            {
                            {"code",tintuc.code.ToString() },
                {"status","active" },
                {"title",  title},
                {"created_at",tintuc.created_at},
                {"updated_at",tintuc.updated_at},
                {"validated_from",tintuc.validated_from},
                      {"content",content},
                      {"category_code",tintuc.category_code},
                      {"category_name",tintuc.category_name},
                            //{"validated_to","" },
                            //    {"link","" }

            };
                        collectionNews.InsertOne(doc);
                        Invoke((MethodInvoker)delegate
                        {

                            ShowLog(String.Format("Thêm thành công bản tin\n"));

                        });
                    }

                }
                catch (Exception ex)
                {

                    Invoke((MethodInvoker)delegate
                    {
                        ShowLog(String.Format(ex.GetType().ToString()));
                        ShowLog(String.Format("Không insert được vào mongodb news\n"));
                        log.Debug("Không insert được vào mongodb news \n" + ex.Message);
                    });
                }

            }

            foreach (var tintucMG in newsMongo)
            {
                try
                {
                    string code = tintucMG["code"].AsString;
                    var data = from c in db.News
                               where c.NewsID.ToString() == code

                               select c;
                    var reslt = data.ToList();

                    if (reslt.Count() != 0 )
                    {
                        if(reslt[0].StatusID == 4)
                        {
                            var update = Builders<BsonDocument>.Update.Set("status", "inactive");
                            collectionNews.UpdateOne(tintucMG, update);

                            Invoke((MethodInvoker)delegate
                            {

                                ShowLog(String.Format("Cập nhật thành công bản tin\n"));

                            });
                        }
                        



                    }
                }
                catch (Exception ex)
                {

                    Invoke((MethodInvoker)delegate
                    {
                        ShowLog(String.Format(ex.GetType().ToString()));
                        ShowLog(String.Format("Không cập nhật được bản tin news\n"));
                        log.Debug("Không cập nhật được bản tin news \n" + ex.Message);
                    });
                }


            }

        }

    }



}
