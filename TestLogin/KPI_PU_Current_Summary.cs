//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace TestLogin
{
    using System;
    using System.Collections.Generic;
    
    public partial class KPI_PU_Current_Summary
    {
        public int Id { get; set; }
        public int PU_Id { get; set; }
        public int KPI_Id { get; set; }
        public int PUKPICT_Id { get; set; }
        public int KPICP_Id { get; set; }
        public int KPICT_Id { get; set; }
        public Nullable<System.DateTime> StartTime { get; set; }
        public Nullable<System.DateTime> EndTime { get; set; }
        public Nullable<System.DateTime> EndTimePlanned { get; set; }
        public Nullable<float> Value { get; set; }
        public bool IsRecalc { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
    }
}
