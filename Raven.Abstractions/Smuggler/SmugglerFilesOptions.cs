using Raven.Abstractions.Data;

namespace Raven.Abstractions.Smuggler
{
    public class SmugglerFilesOptions : SmugglerOptions<FilesConnectionStringOptions>
    {
        public SmugglerFilesOptions()
        {
            StartFilesEtag = Etag.Empty;
            StartFilesDeletionEtag = Etag.Empty;
        }

        public Etag StartFilesEtag { get; set; }
        public Etag StartFilesDeletionEtag { get; set; }

		public bool StripReplicationInformation { get; set; }
    }
}