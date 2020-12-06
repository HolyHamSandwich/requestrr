using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Requestrr.WebApi.Controllers.DownloadClients.Sonarr
{
    public class SonarrSettingsModel : TestSonarrSettingsModel
    {
        [Required]
        public List<SonarrCategorySettings> Categories { get; set; } = new List<SonarrCategorySettings>();
        public bool SearchNewRequests { get; set; }
        public bool MonitorNewRequests { get; set; }
    }

    public enum SeriesType
    {
        Standard,
        Anime,
        Daily
    }

    public class SonarrCategorySettings
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public SeriesType SeriesType { get; set; }
        public int ProfileId { get; set; }
        public string RootFolder { get; set; }
        public int LanguageId { get; set; }
        public int[] Tags { get; set; }
        public bool UseSeasonFolders { get; set; }
    }
}
