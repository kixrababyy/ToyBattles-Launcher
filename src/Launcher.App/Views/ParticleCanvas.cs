using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Launcher.App.Views;

/// <summary>
/// Floating dust-particle background that replicates the Framer
/// FloatingParticlesBackground component.  Rendered via OnRender
/// on a WPF Canvas at ~60 fps.
///
/// Key behaviours (ported from the JS original):
///   • Particles drift freely with slight random perturbation + damping
///   • Each particle has a soft glow halo (shadowBlur equivalent)
///   • Mouse proximity causes particles to glow brighter (eased transition)
///   • Mouse gravity: particles can be attracted toward the cursor
///   • Boundary wrapping — particles wrap across all edges
///   • Blue and yellow color palette matching the ToyBattles brand
/// </summary>
public class ParticleCanvas : Canvas
{
    // ── Tunables (mirroring the Framer property controls) ──────────
    private const int     ParticleCount   = 60;
    private const double  ParticleSizeMin = 1.5;
    private const double  ParticleSizeMax = 4.0;
    private const double  BaseOpacity     = 0.55;
    private const double  GlowRadius      = 14.0;   // px — equivalent to shadowBlur
    private const double  MovementSpeed   = 0.45;
    private const double  MouseInfluence  = 140.0;   // px radius
    private const double  GravityStrength = 40.0;
    private static readonly string MouseGravity = "attract"; // "attract" | "repel" | "none"
    private const double  GlowEaseSpeed   = 0.12;

    // ── Brand colors ──────────────────────────────────────────────
    private static readonly Color[] ParticleColors =
    [
        // Blues
        Color.FromRgb(38, 192, 240),   // #26C0F0  brand sky-blue
        Color.FromRgb(96, 216, 255),   // #60D8FF  light blue
        Color.FromRgb(24, 168, 216),   // #18A8D8  deep blue
        Color.FromRgb(0,  200, 255),   // #00C8FF  cyan
        // Yellows / golds
        Color.FromRgb(255, 184, 0),    // #FFB800  brand gold
        Color.FromRgb(255, 208, 64),   // #FFD040  bright gold
        Color.FromRgb(230, 160, 0),    // #E6A000  amber
    ];

    private readonly List<Particle> _particles = new();
    private readonly Random _rng = new();
    private readonly DispatcherTimer _timer;
    private Point _mouse = new(-9999, -9999);
    private bool _isLoaded;

    public ParticleCanvas()
    {
        IsHitTestVisible = true;  // needed to receive mouse events
        ClipToBounds = true;
        Background = Brushes.Transparent;

        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60 fps
        };
        _timer.Tick += OnTick;

        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    // ── Lifecycle ─────────────────────────────────────────────────
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded)
        {
            InitParticles();
            _isLoaded = true;
        }
        _timer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => _timer.Stop();

    // ── Mouse tracking ────────────────────────────────────────────
    protected override void OnMouseMove(MouseEventArgs e)
    {
        _mouse = e.GetPosition(this);
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        _mouse = new Point(-9999, -9999); // disable influence
        base.OnMouseLeave(e);
    }

    // ── Particle initialisation ───────────────────────────────────
    private void InitParticles()
    {
        var w = ActualWidth  > 0 ? ActualWidth  : 960;
        var h = ActualHeight > 0 ? ActualHeight : 650;

        _particles.Clear();
        for (int i = 0; i < ParticleCount; i++)
            _particles.Add(SpawnParticle(w, h));
    }

    private Particle SpawnParticle(double w, double h)
    {
        var color = ParticleColors[_rng.Next(ParticleColors.Length)];
        return new Particle
        {
            X            = _rng.NextDouble() * w,
            Y            = _rng.NextDouble() * h,
            Vx           = (_rng.NextDouble() - 0.5) * MovementSpeed,
            Vy           = (_rng.NextDouble() - 0.5) * MovementSpeed,
            Size         = ParticleSizeMin + _rng.NextDouble() * (ParticleSizeMax - ParticleSizeMin),
            BaseOpacity  = BaseOpacity,
            Opacity      = BaseOpacity,
            Mass         = _rng.NextDouble() * 0.5 + 0.5,
            Color        = color,
            GlowMultiplier = 1.0,
        };
    }

    // ── Frame update (faithful port of the JS updateParticles) ────
    private void OnTick(object? sender, EventArgs e)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        foreach (var p in _particles)
        {
            // ── Mouse influence ──────────────────────────────────
            var dx   = _mouse.X - p.X;
            var dy   = _mouse.Y - p.Y;
            var dist = Math.Sqrt(dx * dx + dy * dy);

            if (dist < MouseInfluence && dist > 0)
            {
                var force = (MouseInfluence - dist) / MouseInfluence;
                var nx    = dx / dist;
                var ny    = dy / dist;
                var gf    = force * (GravityStrength * 0.001);

                switch (MouseGravity)
                {
                    case "attract":
                        p.Vx += nx * gf;
                        p.Vy += ny * gf;
                        break;
                    case "repel":
                        p.Vx -= nx * gf;
                        p.Vy -= ny * gf;
                        break;
                }

                // Brighten near cursor
                p.Opacity = Math.Min(1.0, p.BaseOpacity + force * 0.4);

                // Ease toward boosted glow
                var targetGlow = 1.0 + force * 2.0;
                p.GlowMultiplier += (targetGlow - p.GlowMultiplier) * GlowEaseSpeed;
            }
            else
            {
                // Fade back to base
                p.Opacity = Math.Max(p.BaseOpacity * 0.3, p.Opacity - 0.02);
                p.GlowMultiplier += (1.0 - p.GlowMultiplier) * 0.08;
            }

            // ── Movement ─────────────────────────────────────────
            p.X += p.Vx;
            p.Y += p.Vy;

            // Subtle random perturbation
            p.Vx += (_rng.NextDouble() - 0.5) * 0.002;
            p.Vy += (_rng.NextDouble() - 0.5) * 0.002;

            // Damping
            p.Vx *= 0.999;
            p.Vy *= 0.999;

            // Boundary wrapping
            if (p.X < 0)  p.X = w;
            if (p.X > w)  p.X = 0;
            if (p.Y < 0)  p.Y = h;
            if (p.Y > h)  p.Y = 0;
        }

        InvalidateVisual();
    }

    // ── Rendering (equivalent to the JS drawParticles) ────────────
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        foreach (var p in _particles)
        {
            if (p.Opacity <= 0.01) continue;

            var center = new Point(p.X, p.Y);
            var alpha  = (byte)Math.Clamp(p.Opacity * 255, 0, 255);
            var glowR  = GlowRadius * p.GlowMultiplier;

            // ── Outer glow halo (shadowBlur equivalent) ──────────
            if (glowR > 1)
            {
                var glowAlpha = (byte)Math.Clamp(alpha * 0.35 * p.GlowMultiplier, 0, 255);
                var glowBrush = new RadialGradientBrush
                {
                    GradientOrigin = new Point(0.5, 0.5),
                    Center         = new Point(0.5, 0.5),
                    RadiusX        = 1, RadiusY = 1,
                    GradientStops  = new GradientStopCollection
                    {
                        new(Color.FromArgb(glowAlpha, p.Color.R, p.Color.G, p.Color.B), 0),
                        new(Color.FromArgb((byte)(glowAlpha * 0.4), p.Color.R, p.Color.G, p.Color.B), 0.5),
                        new(Colors.Transparent, 1),
                    }
                };
                glowBrush.Freeze();
                dc.DrawEllipse(glowBrush, null, center, glowR, glowR);
            }

            // ── Particle core ────────────────────────────────────
            var coreBrush = new SolidColorBrush(
                Color.FromArgb(alpha, p.Color.R, p.Color.G, p.Color.B));
            coreBrush.Freeze();
            dc.DrawEllipse(coreBrush, null, center, p.Size, p.Size);

            // ── Bright center highlight ──────────────────────────
            var highlightAlpha = (byte)Math.Clamp(alpha * 0.7, 0, 255);
            var hlBrush = new SolidColorBrush(
                Color.FromArgb(highlightAlpha, 255, 255, 255));
            hlBrush.Freeze();
            dc.DrawEllipse(hlBrush, null, center, p.Size * 0.35, p.Size * 0.35);
        }
    }

    // ── Particle data ─────────────────────────────────────────────
    private class Particle
    {
        public double X, Y;
        public double Vx, Vy;
        public double Size;
        public double BaseOpacity;
        public double Opacity;
        public double Mass;
        public Color  Color;
        public double GlowMultiplier;
    }
}
