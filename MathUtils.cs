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
        #region Public Constructors

        public Coordinate(Point3D vector)
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

        #region Public Properties

        public double Altitude { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        #endregion Public Properties

        public Point3D ToNZTM()
        {
            var xy = new[] { 0.0, 0.0 };
            var z = new[] { 0.0 };
            Reproject.ReprojectPoints(xy, z, KnownCoordinateSystems.Geographic.World.WGS1984, KnownCoordinateSystems.Projected.NationalGridsNewZealand.NZGD2000NewZealandTransverseMercator, 0, 1);
            return new Point3D { X = xy[0], Y = xy[1], Z = z[0] };
        }
    }

    public struct Point3D
    {
        #region Public Properties

        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public static Point3D operator+ (Point3D l, Point3D r)
        {
            return new Point3D { X = l.X + r.X, Y = l.Y + r.Y, Z = l.Z + r.Z };
        }

        public static Point3D operator -(Point3D l, Point3D r)
        {
            return new Point3D { X = l.X - r.X, Y = l.Y - r.Y, Z = l.Z - r.Z };
        }

        #endregion Public Properties
    }

    public struct Quaternion
    {


        #region Public Properties

        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double W { get; set; }

        #endregion Public Properties
    }

    /*struct MapCamera {
        MapCamera(object n, object u, object o, object s, object l, object a, object y, object p, object w, object b) {
            var tt = () => {
                var l, h, b, p;
                if (!ni) {
                    ft = s === 0 && et === -90 && a === 0, st = ft || ct <= ot, at = st && ft ? f.simpleGround : ei.fromNormal(rt, it.getUpVector(rt));
                    var n = f.simpleWorldToFrustumNoZ, i = 1 / g, u = g * 1000.0;
                    if (ut = it.getNadirMatrix(rt), ft || (ut = e.multiply(ut, e.rotationZ(-pt), e.rotationX(-(et + 90)), e.rotationZ(-lt))), st) ft ? (n.m22 = n.m11 = i, n.m33 = i * -0.001, n.offsetX = ut.offsetX * i, n.offsetY = ut.offsetY * i) : n = e.multiply(ut, e.scaling(i, i, 1 / u)); else {
                        var y = Math.pow(2, wt) * g, o = 1 / ct, w = o / y;
                        u = Math.min(o * 0.99, u), l = o - u, h = o + u, n = e.multiply(ut, e.translation(bt), e.translation(0, 0, o), new e(w, 0, 0, 0, 0, w, 0, 0, 0, 0, h / (h - l), 1, 0, 0, l * h / (l - h), 0), e.scaling(y / g, y / g, 1)), b = n.transform(rt), n.multiplyBy(e.translation(r.multiply(b, -1)));
                    }
                    ft? ht = n.clone() : (vt = n.invert().projectOnPlane(at), ht = vt.invert()), ft ? (k = ht.clone(), k.m11 *= d, k.m22 *= d, k.m33 *= d, k.offsetX *= d, k.offsetY *= d, p = g / d, nt = ut.clone(), nt.m11 *= p, nt.m22 *= p, nt.m33 *= u / d, nt.offsetX *= -1, nt.offsetY *= -1) : (k = e.multiply(ht, e.scaling(d)), nt = k.invert()), yt = it.toLocation(rt), kt = f.widthToMercatorZoom(g, it, rt), dt = it.unitsToMeters(rt, g) / t.zoomOriginWidth, gt = !st || Math.abs(et + 90) > ot || it !== c, ni = !0;
                }
            }
            var ti = this;
            var it = n, rt = u, g = o, pt = s || 0, et = l === h ? -90 : l, lt = a || 0, ct = y || 0, wt = p || 0, bt = w || new r(0, 0, 0), ft, st, at, ut, ht, vt, k, nt, yt, kt, dt, gt, d = b || t.zoomOriginWidth, ni = !1;
            this.getCrs = function() {
                return it;
            }, this.getLookAt = function() {
                return rt;
            }, this.getWidth = function() {
                return g;
            }, this.getHeading = function() {
                return pt;
            }, this.getPitch = function() {
                return et;
            }, this.getRoll = function() {
                return lt;
            }, this.getDistanceInverse = function() {
                return ct;
            }, this.getZoomDelta = function() {
                return wt;
            }, this.getPerspectiveOrigin = function() {
                return bt;
            }, this.getZoomOriginWidth = function() {
                return d;
            }, this.getGroundPlane = function() {
                return tt(), this.getGroundPlane = function() {
                    return at;
                }, at;
            }, this.getWorldToViewport = function() {
                return tt(), this.getWorldToViewport = function() {
                    return k;
                }, k;
            }, this.getViewportToWorld = function() {
                return tt(), this.getViewportToWorld = function() {
                    return nt;
                }, nt;
            }, this.getWorldToFrustum = function() {
                return tt(), this.getWorldToFrustum = function() {
                    return ht;
                }, ht;
            }, this.getFrustumToWorld = function() {
                return tt(), this.getFrustumToWorld = function() {
                    return vt;
                }, vt;
            }, this.getCenter = function() {
                return tt(), this.getCenter = function() {
                    return yt;
                }, yt;
            }, this.getMercatorZoom = function() {
                return tt(), this.getMercatorZoom = function() {
                    return kt;
                }, kt;
            }, this.getMetersPerPixel = function() {
                return tt(), this.getMetersPerPixel = function() {
                    return dt;
                }, dt;
            }, this.getNeedsElevation = function() {
                return tt(), this.getNeedsElevation = function() {
                    return gt;
                }, gt;
            }, this.getAlignedCamera = function(n, t, r, u) {
                var s, l;
                tt();
                var v = g / Math.Pow(2, n), o, h = null, c = 0, a = null;
                return t ? (o = t, st || (a = e.multiply(ut, e.translation(bt)), h = a.transform(o), c = n + wt)) : o = rt, s = pt, u && (s = s + u), c = 0, l = new f(it, o, v, s, et, lt, ct, c, h, d), r && (o = l.getViewportToWorld().transform(new i(-r.x, -r.y)), st || (h = a.transform(o)), l = new f(it, o, v, s, et, lt, ct, c, h, d)), l;
            };
        }

        public static MapCamera fromDirectionVector(object n, object t, object i, object u, object o, object s) {
            var y;
            var a;
            var p = n.getNadirMatrix(t);
            var h = p.transform(Point3D.add(t, u)).normalize();
            var c = 0;
            if (h.x !== 0 || h.y !== 0) {
                directionXY = new Point3D(h.x, h.y, 0).normalize();
                c = Math.Acos(Point3D.dot(directionXY, new Point3D(0, -1, 0))) * l.degreesPerRadian;
                directionXY.x < 0 && (c = 360 - c);
            }
            var w = (Math.PI / 2 - Math.Acos(Point3D.dot(h, new Point3D(0, 0, -1)))) * l.degreesPerRadian;
            var a = 0;
            var b = e.multiply(p, e.rotationZ(-c), e.rotationX(-(w + 90)));
            var v = b.transform(Point3D.add(t, o));
            if (v.x != 0 || v.y != 0) {
                y = new Point3D(v.x, v.y, 0).normalize();
                a = Math.Acos(Point3D.dot(y, new Point3D(0, -1, 0))) * l.degreesPerRadian;
                if (y.x < 0) {
                    a = 360 - a;
                }
            }
            return new MapCamera(n, t, i, c, w, a, s);
        }
    }*/

    internal static class MathUtils
    {
        #region Public Fields

        public const double degreesPerRadian = 180 / Math.PI;
        public const double earthRadiusInMeters = 6378137;

        #endregion Public Fields
    }
}
