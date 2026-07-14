namespace DocMgr.Models.YearlyArchive
{
    public class ArchiveRegisterPrintData
    {
        public string FormNo { get; set; } = string.Empty;
        public string MaterialName { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public string SourceType { get; set; } = string.Empty;
        public string ProvideUnit { get; set; } = string.Empty;
        public List<string> ItemLines { get; set; } = new();
        public List<string> ProofLines { get; set; } = new();
        public string RetainedHardDiskRegistration { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public string OtherRequests { get; set; } = string.Empty;
        public string Dept { get; set; } = string.Empty;
        public string Applicant { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string ProdOpinion { get; set; } = string.Empty;
        public string RndOpinion { get; set; } = string.Empty;
        public string DeptLeaderApproval { get; set; } = string.Empty;
        public string DeputyOpinion { get; set; } = string.Empty;
        public string ProdFull { get; set; } = string.Empty;
        public string RndFull { get; set; } = string.Empty;
        public string DeputyFull { get; set; } = string.Empty;
        public string DeliverFull { get; set; } = string.Empty;
        public string AdminFull { get; set; } = string.Empty;
        public string OpticalDiscLedgerSummary { get; set; } = string.Empty;
    }
}
