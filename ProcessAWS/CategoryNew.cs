//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace ProcessAWS
{
    using System;
    using System.Collections.Generic;
    
    public partial class CategoryNew
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public CategoryNew()
        {
            this.News = new HashSet<News>();
        }
    
        public int CategoryNewID { get; set; }
        public string CategoryNewName { get; set; }
        public Nullable<int> Languages_ID { get; set; }
        public Nullable<bool> IsFree { get; set; }
        public Nullable<int> CategoryNew_ParrentID { get; set; }
    
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<News> News { get; set; }
    }
}
