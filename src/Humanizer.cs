using System;
using System.Collections.Generic;

namespace GhostMacro
{
    /// <summary>"İnsan gibi oynat" için rastgele süre, konum ve fare yolu üretir.</summary>
    internal sealed class Humanizer
    {
        readonly Random rnd;

        public Humanizer(Random r) { rnd = r; }

        /// <summary>Standart normal dağılım (Box-Muller).</summary>
        public double Gauss()
        {
            double u1 = 1.0 - rnd.NextDouble(), u2 = rnd.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
        }

        /// <summary>Çoğunlukla ortalamaya yakın, [min, max] aralığında süre.</summary>
        public double Range(double min, double max)
        {
            double mid = (min + max) / 2, half = (max - min) / 2;
            double g = Gauss() / 2.5;                 // ~%99 aralık içinde
            if (g < -1) g = -1; else if (g > 1) g = 1;
            return mid + g * half;
        }

        /// <summary>Bekleme çarpanı: 1 ± pct/100, normal dağılımlı.</summary>
        public double DelayFactor(int pct)
        {
            double g = Gauss() / 2;
            if (g < -1) g = -1; else if (g > 1) g = 1;
            return 1 + g * pct / 100.0;
        }

        /// <summary>Tıklama noktasını en çok radius piksel kaydırır.</summary>
        public void Offset(ref int x, ref int y, int radius)
        {
            double a = rnd.NextDouble() * 2 * Math.PI;
            double r = Math.Sqrt(rnd.NextDouble()) * radius;
            x += (int)Math.Round(Math.Cos(a) * r);
            y += (int)Math.Round(Math.Sin(a) * r);
        }

        /// <summary>Ara sıra (yaklaşık %3 adımda) kısa bir duraklama, yoksa 0.</summary>
        public double Hesitation() => rnd.NextDouble() < 0.03 ? Range(300, 1500) : 0;

        /// <summary>Mesafeye göre doğal hareket süresi (ms).</summary>
        public static double MoveTime(double dist) => Math.Min(900, 120 + dist * 0.45);

        /// <summary>
        /// Başlangıçtan hedefe kavisli yol; uzak hedeflerde biraz geçip geri döner.
        /// Son nokta her zaman tam hedeftir.
        /// </summary>
        public List<Native.POINT> Path(double sx, double sy, int tx, int ty, int n)
        {
            var pts = new List<Native.POINT>();
            double dx = tx - sx, dy = ty - sy, dist = Math.Sqrt(dx * dx + dy * dy);
            n = Math.Max(1, n);
            if (dist < 1) { pts.Add(new Native.POINT { X = tx, Y = ty }); return pts; }

            // dik yönde kavis
            double nx = -dy / dist, ny = dx / dist;
            double bend = Range(-0.18, 0.18) * dist;
            double c1x = sx + dx * 0.3 + nx * bend, c1y = sy + dy * 0.3 + ny * bend;
            double c2x = sx + dx * 0.7 + nx * bend * 0.6, c2y = sy + dy * 0.7 + ny * bend * 0.6;

            // uzak hedefte küçük taşma
            bool over = dist > 200 && rnd.NextDouble() < 0.6;
            double ox = tx, oy = ty;
            if (over)
            {
                double o = Range(4, Math.Min(14, dist * 0.04));
                ox = tx + dx / dist * o + nx * Range(-3, 3);
                oy = ty + dy / dist * o + ny * Range(-3, 3);
            }
            int main = over ? Math.Max(1, (int)(n * 0.85)) : n;

            for (int k = 1; k <= main; k++)
            {
                double t = (double)k / main;
                double e = t < 0.5 ? 2 * t * t : 1 - Math.Pow(-2 * t + 2, 2) / 2;
                double u = 1 - e;
                double x = u * u * u * sx + 3 * u * u * e * c1x + 3 * u * e * e * c2x + e * e * e * ox;
                double y = u * u * u * sy + 3 * u * u * e * c1y + 3 * u * e * e * c2y + e * e * e * oy;
                pts.Add(new Native.POINT { X = (int)Math.Round(x), Y = (int)Math.Round(y) });
            }
            int back = n - main;
            for (int k = 1; k <= back; k++)
            {
                double t = (double)k / back;
                pts.Add(new Native.POINT { X = (int)Math.Round(ox + (tx - ox) * t), Y = (int)Math.Round(oy + (ty - oy) * t) });
            }
            pts[pts.Count - 1] = new Native.POINT { X = tx, Y = ty };
            return pts;
        }
    }
}
