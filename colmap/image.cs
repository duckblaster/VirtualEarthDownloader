namespace Downloader.colmap
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class image
    {
        [Key]
        public long image_id { get; set; }

        [Required]
        [StringLength(2147483647)]
        public string name { get; set; }

        public long camera_id { get; set; }

        [Column(TypeName = "real")]
        public double? prior_qw { get; set; }

        [Column(TypeName = "real")]
        public double? prior_qx { get; set; }

        [Column(TypeName = "real")]
        public double? prior_qy { get; set; }

        [Column(TypeName = "real")]
        public double? prior_qz { get; set; }

        [Column(TypeName = "real")]
        public double? prior_tx { get; set; }

        [Column(TypeName = "real")]
        public double? prior_ty { get; set; }

        [Column(TypeName = "real")]
        public double? prior_tz { get; set; }

        public virtual camera camera { get; set; }

        public virtual descriptor descriptor { get; set; }

        public virtual keypoint keypoint { get; set; }
    }
}
