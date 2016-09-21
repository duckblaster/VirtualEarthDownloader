using DotSpatial.Projections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Downloader
{
    public struct Coordinate
    {
        #region Public Fields

        public double Altitude;

        public double Latitude;

        public double Longitude;

        #endregion Public Fields

        #region Public Constructors

        public Coordinate(Vector3 vector)
        {
            const double flattening = 1 / 298.257222101;
            const double eccentricity = flattening * (2 - flattening);
            const double earthRadius = MathUtils.earthRadiusInMeters;
            var x = vector.X; // +0Degrees/-180Degrees
            var y = vector.Y; // +90Degrees/-90Degrees
            var z = vector.Z; // +North/-South
            var radiusOnSphere = Math.Sqrt(x * x + y * y);
            var longitudeRadians = Math.Atan2(y, x);

            var radiusOnEllipsoid = radiusOnSphere * (1 - flattening);
            var invEquatorDist = 1 / Math.Sqrt(z * z + radiusOnEllipsoid * radiusOnEllipsoid);
            var latitudeRadians = Math.Atan2(z, radiusOnEllipsoid);
            var ratioRadiusToEquatorDist = radiusOnEllipsoid * invEquatorDist;
            var localEarthRadius = earthRadius;
            var altitude = radiusOnSphere > 1 ? radiusOnSphere / ratioRadiusToEquatorDist - localEarthRadius : (z > 0 ? z - earthRadius * (1 - flattening) : -z - earthRadius * (1 - flattening));
            for (var d = 3; d > 0; d--)
            {
                radiusOnEllipsoid = radiusOnSphere * (1 - eccentricity * localEarthRadius / (localEarthRadius + altitude));
                invEquatorDist = 1 / Math.Sqrt(z * z + radiusOnEllipsoid * radiusOnEllipsoid);
                latitudeRadians = Math.Atan2(z, radiusOnEllipsoid);
                var ratioZToRadius = z * invEquatorDist;
                ratioRadiusToEquatorDist = radiusOnEllipsoid * invEquatorDist;
                localEarthRadius = earthRadius / Math.Sqrt(1 - eccentricity * ratioZToRadius * ratioZToRadius);
                altitude = radiusOnSphere > 1 ? radiusOnSphere / ratioRadiusToEquatorDist - localEarthRadius : (z > 0 ? z - earthRadius * (1 - flattening) : -z - earthRadius * (1 - flattening));
            }
            Latitude = latitudeRadians * MathUtils.degreesPerRadian;
            Longitude = longitudeRadians * MathUtils.degreesPerRadian;
            Altitude = altitude;
        }

        #endregion Public Constructors

        #region Public Methods

        public Vector3 ToNZTM()
        {
            var xy = new[] { Longitude, Latitude };
            var z = new[] { Altitude };
            Reproject.ReprojectPoints(xy, z, KnownCoordinateSystems.Geographic.World.WGS1984, KnownCoordinateSystems.Projected.NationalGridsNewZealand.NZGD2000NewZealandTransverseMercator, 0, 1);
            return new Vector3 { X = xy[0], Y = xy[1], Z = z[0] };
        }

        #endregion Public Methods
    }

    public struct Matrix
    {
        /// <summary>
        /// A first row and first column value.
        /// </summary>

        #region Public Fields

        public double M11;

        /// <summary>
        /// A first row and second column value.
        /// </summary>

        public double M12;

        /// <summary>
        /// A first row and third column value.
        /// </summary>

        public double M13;

        /// <summary>
        /// A second row and first column value.
        /// </summary>

        public double M21;

        /// <summary>
        /// A second row and second column value.
        /// </summary>

        public double M22;

        /// <summary>
        /// A second row and third column value.
        /// </summary>

        public double M23;

        /// <summary>
        /// A third row and first column value.
        /// </summary>

        public double M31;

        /// <summary>
        /// A third row and second column value.
        /// </summary>

        public double M32;

        /// <summary>
        /// A third row and third column value.
        /// </summary>

        public double M33;

        #endregion Public Fields

        #region Public Methods

        public void makeRotationDir(Vector3 direction, Vector3 up)
        {
            Vector3 xaxis = Vector3.Cross(up, direction);
            xaxis.Normalize();

            Vector3 yaxis = Vector3.Cross(direction, xaxis);
            yaxis.Normalize();

            M11 = xaxis.X;
            M21 = yaxis.X;
            M31 = direction.X;

            M12 = xaxis.Y;
            M22 = yaxis.Y;
            M32 = direction.Y;

            M13 = xaxis.Z;
            M23 = yaxis.Z;
            M33 = direction.Z;
        }

        #endregion Public Methods
    }

    public struct Quaternion
    {
        #region Public Fields

        public double W;
        public double X;
        public double Y;
        public double Z;

        #endregion Public Fields

        #region Public Methods

        /// <summary>
        /// Creates a new <see cref="Quaternion"/> from the specified <see cref="Matrix"/>.
        /// </summary>
        /// <param name="matrix">The rotation matrix.</param>
        /// <returns>A quaternion composed from the rotation part of the matrix.</returns>
        public static Quaternion CreateFromRotationMatrix(Matrix matrix)
        {
            Quaternion quaternion;
            double sqrt;
            double half;
            double scale = matrix.M11 + matrix.M22 + matrix.M33;

            if (scale > 0.0f)
            {
                sqrt = Math.Sqrt(scale + 1.0f);
                quaternion.W = sqrt * 0.5f;
                sqrt = 0.5f / sqrt;

                quaternion.X = (matrix.M23 - matrix.M32) * sqrt;
                quaternion.Y = (matrix.M31 - matrix.M13) * sqrt;
                quaternion.Z = (matrix.M12 - matrix.M21) * sqrt;

                return quaternion;
            }
            if ((matrix.M11 >= matrix.M22) && (matrix.M11 >= matrix.M33))
            {
                sqrt = Math.Sqrt(1.0f + matrix.M11 - matrix.M22 - matrix.M33);
                half = 0.5f / sqrt;

                quaternion.X = 0.5f * sqrt;
                quaternion.Y = (matrix.M12 + matrix.M21) * half;
                quaternion.Z = (matrix.M13 + matrix.M31) * half;
                quaternion.W = (matrix.M23 - matrix.M32) * half;

                return quaternion;
            }
            if (matrix.M22 > matrix.M33)
            {
                sqrt = Math.Sqrt(1.0f + matrix.M22 - matrix.M11 - matrix.M33);
                half = 0.5f / sqrt;

                quaternion.X = (matrix.M21 + matrix.M12) * half;
                quaternion.Y = 0.5f * sqrt;
                quaternion.Z = (matrix.M32 + matrix.M23) * half;
                quaternion.W = (matrix.M31 - matrix.M13) * half;

                return quaternion;
            }
            sqrt = Math.Sqrt(1.0f + matrix.M33 - matrix.M11 - matrix.M22);
            half = 0.5f / sqrt;

            quaternion.X = (matrix.M31 + matrix.M13) * half;
            quaternion.Y = (matrix.M32 + matrix.M23) * half;
            quaternion.Z = 0.5f * sqrt;
            quaternion.W = (matrix.M12 - matrix.M21) * half;

            return quaternion;
        }

        #endregion Public Methods
    }

    public struct Vector3
    {
        #region Public Fields

        public double X;
        public double Y;
        public double Z;

        #endregion Public Fields

        #region Private Fields

        private static Vector3 zero = new Vector3(0.0, 0.0, 0.0);

        #endregion Private Fields

        #region Public Constructors

        public Vector3(double x, double y, double z) : this()
        {
            X = x;
            Y = y;
            Z = z;
        }

        #endregion Public Constructors

        #region Public Methods

        /// <summary>
        /// Computes the cross product of two vectors.
        /// </summary>
        /// <param name="vector1">The first vector.</param>
        /// <param name="vector2">The second vector.</param>
        /// <returns>The cross product of two vectors.</returns>
        public static Vector3 Cross(Vector3 vector1, Vector3 vector2)
        {
            var x = vector1.Y * vector2.Z - vector2.Y * vector1.Z;
            var y = -(vector1.X * vector2.Z - vector2.X * vector1.Z);
            var z = vector1.X * vector2.Y - vector2.X * vector1.Y;
            var result = new Vector3();
            result.X = x;
            result.Y = y;
            result.Z = z;
            return result;
        }

        /// <summary>
        /// Returns the distance between two vectors.
        /// </summary>
        /// <param name="value1">The first vector.</param>
        /// <param name="value2">The second vector.</param>
        /// <returns>The distance between two vectors.</returns>
        public static double Distance(Vector3 value1, Vector3 value2)
        {
            double result;
            DistanceSquared(ref value1, ref value2, out result);
            return Math.Sqrt(result);
        }

        /// <summary>
        /// Returns the distance between two vectors.
        /// </summary>
        /// <param name="value1">The first vector.</param>
        /// <param name="value2">The second vector.</param>
        /// <param name="result">The distance between two vectors as an output parameter.</param>
        public static void Distance(ref Vector3 value1, ref Vector3 value2, out double result)
        {
            DistanceSquared(ref value1, ref value2, out result);
            result = Math.Sqrt(result);
        }

        /// <summary>
        /// Returns the squared distance between two vectors.
        /// </summary>
        /// <param name="value1">The first vector.</param>
        /// <param name="value2">The second vector.</param>
        /// <returns>The squared distance between two vectors.</returns>
        public static double DistanceSquared(Vector3 value1, Vector3 value2)
        {
            return (value1.X - value2.X) * (value1.X - value2.X) +
                    (value1.Y - value2.Y) * (value1.Y - value2.Y) +
                    (value1.Z - value2.Z) * (value1.Z - value2.Z);
        }

        /// <summary>
        /// Returns the squared distance between two vectors.
        /// </summary>
        /// <param name="value1">The first vector.</param>
        /// <param name="value2">The second vector.</param>
        /// <param name="result">The squared distance between two vectors as an output parameter.</param>
        public static void DistanceSquared(ref Vector3 value1, ref Vector3 value2, out double result)
        {
            result = (value1.X - value2.X) * (value1.X - value2.X) +
                     (value1.Y - value2.Y) * (value1.Y - value2.Y) +
                     (value1.Z - value2.Z) * (value1.Z - value2.Z);
        }

        /// <summary>
        /// Creates a new <see cref="Vector3"/> that contains a normalized values from another vector.
        /// </summary>
        /// <param name="value">Source <see cref="Vector3"/>.</param>
        /// <returns>Unit vector.</returns>
        public static Vector3 Normalize(Vector3 value)
        {
            double factor = Distance(value, zero);
            factor = 1f / factor;
            return new Vector3(value.X * factor, value.Y * factor, value.Z * factor);
        }

        public static Vector3 operator -(Vector3 l, Vector3 r)
        {
            return new Vector3 { X = l.X - r.X, Y = l.Y - r.Y, Z = l.Z - r.Z };
        }

        public static Vector3 operator +(Vector3 l, Vector3 r)
        {
            return new Vector3 { X = l.X + r.X, Y = l.Y + r.Y, Z = l.Z + r.Z };
        }

        public void Normalize()
        {
            double factor = Distance(this, zero);
            factor = 1f / factor;
            X = X * factor;
            Y = Y * factor;
            Z = Z * factor;
        }

        #endregion Public Methods
    }

    public static class MathUtils
    {
        #region Public Fields

        public const double degreesPerRadian = 180 / Math.PI;
        public const double earthRadiusInMeters = 6378137;

        #endregion Public Fields

        #region Public Methods

        public static Matrix makeRotationDir(Vector3 direction, Vector3 up)
        {
            var matrix = new Matrix();
            Vector3 xaxis = Vector3.Cross(up, direction);
            xaxis.Normalize();

            Vector3 yaxis = Vector3.Cross(direction, xaxis);
            yaxis.Normalize();

            matrix.M11 = xaxis.X;
            matrix.M21 = yaxis.X;
            matrix.M31 = direction.X;

            matrix.M12 = xaxis.Y;
            matrix.M22 = yaxis.Y;
            matrix.M32 = direction.Y;

            matrix.M13 = xaxis.Z;
            matrix.M23 = yaxis.Z;
            matrix.M33 = direction.Z;

            return matrix;
        }

        #endregion Public Methods
    }
}
