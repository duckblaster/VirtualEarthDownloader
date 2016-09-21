namespace Downloader
{
    public class ImageTransform
    {
        #region Public Fields

        public Vector3 cameraPos;
        public Quaternion cameraRot;

        #endregion Public Fields

        #region Public Constructors

        public ImageTransform(Vector3 cameraPos, Quaternion cameraRot)
        {
            this.cameraPos = cameraPos;
            this.cameraRot = cameraRot;
        }

        #endregion Public Constructors
    }
}
