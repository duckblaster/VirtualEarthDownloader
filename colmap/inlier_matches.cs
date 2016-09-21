namespace Downloader.colmap
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class inlier_matches
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long pair_id { get; set; }

        public long rows { get; set; }

        public long cols { get; set; }

        [MaxLength(2147483647)]
        public byte[] data { get; set; }

        public long config { get; set; }
    }
}
