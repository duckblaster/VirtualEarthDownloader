namespace Downloader.colmap
{
    using System;
    using System.Data.Entity;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Linq;

    public partial class Colmap : DbContext
    {
        public Colmap()
            : base("name=Colmap")
        {
        }

        public virtual DbSet<camera> cameras { get; set; }
        public virtual DbSet<descriptor> descriptors { get; set; }
        public virtual DbSet<image> images { get; set; }
        public virtual DbSet<inlier_matches> inlier_matches { get; set; }
        public virtual DbSet<keypoint> keypoints { get; set; }
        public virtual DbSet<match> matches { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<camera>()
                .HasMany(e => e.images)
                .WithRequired(e => e.camera)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<image>()
                .HasOptional(e => e.descriptor)
                .WithRequired(e => e.image);

            modelBuilder.Entity<image>()
                .HasOptional(e => e.keypoint)
                .WithRequired(e => e.image);
        }
    }
}
