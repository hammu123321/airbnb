//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Replica.Models
{
    using System;
    using System.Collections.Generic;
    
    public partial class ViewHistory
    {
        public int view_id { get; set; }
        public int user_id { get; set; }
        public int place_id { get; set; }
        public int view_count { get; set; }
    
        public virtual Place Place { get; set; }
        public virtual User User { get; set; }
    }
}
