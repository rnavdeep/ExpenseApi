using System;
namespace NSWalks.API.Models.Domain
{
	public class Document
	{
		public Document()
		{
		}
        public Guid Id { get; set; }
        public string FileName { get; set; }
        public string FileExtension { get; set; }
        public string S3Url { get; set; }
        public long Size { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        public Guid UserId { get; set; }
        public User User { get; set; } // Navigation Property
    }
}

