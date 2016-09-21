namespace Downloader.colmap
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class camera
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public camera()
        {
            images = new HashSet<image>();
        }

        [Key]
        public long camera_id { get; set; }

        public long model { get; set; }

        public long width { get; set; }

        public long height { get; set; }

        [Column("params")]
        [MaxLength(2147483647)]
        public byte[] _params { get; set; }

        public long prior_focal_length { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<image> images { get; set; }
    }
}
