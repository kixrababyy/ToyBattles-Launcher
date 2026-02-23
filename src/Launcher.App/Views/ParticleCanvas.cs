using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Launcher.App.Views;

/// <summary>
/// Lightweight floating particles rendered directly onto a WPF Canvas.
/// Particles drift slowly, fade in/out, and give the launcher a living feel.
/// </summary>
public class ParticleCanvas : Canvas
{
    private readonly List<Particle> _particles = new();
    private readonly Random _rng = new();
    private readonly DispatcherTimer _timer;
    private bool _isInitialized;

    // Particle colors matching the brand palette
    private static readonly Color[] ParticleColors =
    [
        Color.FromArgb(40, 38, 192, 240),   // AccentBlue
        Color.FromArgb(30, 96, 216, 255),    // AccentBlueLight
        Color.FromArgb(25, 255, 184, 0),     // AccentGold
        Color.FromArgb(20, 0, 200, 255),     // Cyan
        Color.FromArgb(15, 255, 255, 255),   // White sparkle
    ];

    private const int MaxParticles = 45;

    public ParticleCanvas()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
        Background = Brushes.Transparent;

        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60fps
        };
        _timer.Tick += OnTick;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized)
        {
            SpawnInitialParticles();
            _isInitialized = true;
        }
        _timer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
    }

    private void SpawnInitialParticles()
    {
        var w = ActualWidth > 0 ? ActualWidth : 960;
        var h = ActualHeight > 0 ? ActualHeight : 650;

        for (int i = 0; i < MaxParticles; i++)
        {
            _particles.Add(CreateParticle(w, h, randomizeLifePhase: true));
        }
    }

    private Particle CreateParticle(double areaW, double areaH, bool randomizeLifePhase = false)
    {
        var color = ParticleColors[_rng.Next(ParticleColors.Length)];
        var size = 1.5 + _rng.NextDouble() * 3.5; // 1.5 - 5px
        var lifetime = 6.0 + _rng.NextDouble() * 10.0; // 6-16 seconds

        var p = new Particle
        {
            X = _rng.NextDouble() * areaW,
            Y = _rng.NextDouble() * areaH,
            VelocityX = (_rng.NextDouble() - 0.5) * 0.6,  // slow drift
            VelocityY = -0.15 - _rng.NextDouble() * 0.4,  // gentle upward float
            Size = size,
            Color = color,
            Lifetime = lifetime,
            Age = randomizeLifePhase ? _rng.NextDouble() * lifetime : 0,
            MaxOpacity = 0.3 + _rng.NextDouble() * 0.5,
            // Slight lateral oscillation
            DriftAmplitude = 0.2 + _rng.NextDouble() * 0.5,
            DriftSpeed = 0.5 + _rng.NextDouble() * 1.5,
            DriftPhase = _rng.NextDouble() * Math.PI * 2,
        };
        return p;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        var dt = 0.016; // ~16ms per frame

        // Update particles
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.Age += dt;

            if (p.Age >= p.Lifetime)
            {
                // Respawn at bottom
                _particles[i] = CreateParticle(w, h);
                _particles[i].Y = h + 10; // Start below viewport
                continue;
            }

            // Oscillating drift
            var driftOffset = Math.Sin(p.Age * p.DriftSpeed + p.DriftPhase) * p.DriftAmplitude;
            p.X += (p.VelocityX + driftOffset * dt) * 1.0;
            p.Y += p.VelocityY;

            // Wrap horizontally
            if (p.X < -10) p.X = w + 10;
            if (p.X > w + 10) p.X = -10;
        }

        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        foreach (var p in _particles)
        {
            // Fade in/out based on life phase
            var lifeFraction = p.Age / p.Lifetime;
            double opacity;
            if (lifeFraction < 0.15)
                opacity = lifeFraction / 0.15; // fade in
            else if (lifeFraction > 0.8)
                opacity = (1.0 - lifeFraction) / 0.2; // fade out
            else
                opacity = 1.0;

            opacity *= p.MaxOpacity;
            if (opacity <= 0.01) continue;

            var color = Color.FromArgb(
                (byte)(p.Color.A * opacity),
                p.Color.R, p.Color.G, p.Color.B);

            var brush = new SolidColorBrush(color);
            brush.Freeze();

            dc.DrawEllipse(brush, null, new Point(p.X, p.Y), p.Size, p.Size);

            // Small glow halo for larger particles
            if (p.Size > 3.0)
            {
                var glowColor = Color.FromArgb(
                    (byte)(p.Color.A * opacity * 0.3),
                    p.Color.R, p.Color.G, p.Color.B);
                var glowBrush = new SolidColorBrush(glowColor);
                glowBrush.Freeze();
                dc.DrawEllipse(glowBrush, null, new Point(p.X, p.Y), p.Size * 3, p.Size * 3);
            }
        }
    }

    private class Particle
    {
        public double X;
        public double Y;
        public double VelocityX;
        public double VelocityY;
        public double Size;
        public Color Color;
        public double Lifetime;
        public double Age;
        public double MaxOpacity;
        public double DriftAmplitude;
        public double DriftSpeed;
        public double DriftPhase;
    }
}
