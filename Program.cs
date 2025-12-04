using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace FishTankGame
{
    // Main Entry Point
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new GameForm());
        }
    }

    // Fish Object Class
    public class Fish
    {
        // Properties
        public float X { get; set; }
        public float Y { get; set; }
        public float DX { get; set; }
        public float DY { get; set; }
        public float Size { get; set; }
        public Color Color { get; set; }

        // Feeding/Lure Logic
        public PointF? TargetPosition { get; set; }

        // Difficulty Properties
        public float SpeedMultiplier { get; private set; }
        private float BaseSpeed;

        private static Random rand = new Random();

        public Fish(float x, float y, float speedMult, Color color)
        {
            X = x;
            Y = y;
            Size = 30f; // Initial size
            Color = color;
            SpeedMultiplier = speedMult;

            // Random velocity (-2 to 2) scaled by multiplier
            DX = (float)(rand.NextDouble() * 4 - 2) * SpeedMultiplier;
            DY = (float)(rand.NextDouble() * 4 - 2) * SpeedMultiplier;

            // Ensure minimum speed (also scaled)
            float minSpeed = 0.5f * SpeedMultiplier;
            if (Math.Abs(DX) < minSpeed) DX = DX > 0 ? minSpeed : -minSpeed;
            if (Math.Abs(DY) < minSpeed) DY = DY > 0 ? minSpeed : -minSpeed;

            // Calculate BaseSpeed
            BaseSpeed = (float)Math.Sqrt(DX * DX + DY * DY);
        }

        // Clone method for Reproduction
        public Fish Clone()
        {
            // Clone keeps same speed multiplier and color
            Fish newFish = new Fish(this.X, this.Y, this.SpeedMultiplier, this.Color);
            newFish.Size = this.Size;
            return newFish;
        }

        public void Move(Rectangle bounds)
        {
            if (TargetPosition.HasValue)
            {
                // Lure Mode: Move towards target at 3x speed
                float tx = TargetPosition.Value.X;
                float ty = TargetPosition.Value.Y;
                float cx = X + Size / 2;
                float cy = Y + Size / 2;

                float vecX = tx - cx;
                float vecY = ty - cy;
                float dist = (float)Math.Sqrt(vecX * vecX + vecY * vecY);

                if (dist > 1.0f)
                {
                    float speed = BaseSpeed * 3.0f;
                    float dirX = vecX / dist;
                    float dirY = vecY / dist;

                    X += dirX * speed;
                    Y += dirY * speed;
                }

                // Clamp
                if (X < bounds.Left) X = bounds.Left;
                else if (X + Size > bounds.Right) X = bounds.Right - Size;
                if (Y < bounds.Top) Y = bounds.Top;
                else if (Y + Size > bounds.Bottom) Y = bounds.Bottom - Size;
            }
            else
            {
                // Normal Mode
                X += DX;
                Y += DY;

                // Bounce
                if (X < bounds.Left) { X = bounds.Left; DX = -DX; }
                else if (X + Size > bounds.Right) { X = bounds.Right - Size; DX = -DX; }
                if (Y < bounds.Top) { Y = bounds.Top; DY = -DY; }
                else if (Y + Size > bounds.Bottom) { Y = bounds.Bottom - Size; DY = -DY; }
            }
        }

        public void Draw(Graphics g)
        {
            using (Brush b = new SolidBrush(Color))
            {
                g.FillEllipse(b, X, Y, Size, Size);
            }
            g.DrawEllipse(Pens.Black, X, Y, Size, Size);
        }

        public void Eat(float growthMultiplier)
        {
            // Base growth is 10% (0.1).
            // Scaled by multiplier: 0.1 * growthMultiplier
            float growthRate = 0.1f * growthMultiplier;
            Size *= (1.0f + growthRate);
        }

        public PointF Center => new PointF(X + Size / 2, Y + Size / 2);
        public float Radius => Size / 2;
    }

    // Main Game Form
    public class GameForm : Form
    {
        private List<Fish> fishes;
        private Timer gameTimer;
        private Timer foodTimer;

        // Areas
        private const int FoodPanelHeight = 100;
        private const int WindowWidth = 500;
        private const int WindowHeight = 500;
        private Rectangle foodPanelRect;
        private Rectangle fishTankRect;

        // Food System
        private int foodCount = 0;

        // Interaction
        private bool isDragging = false;
        private Point currentMousePos;

        // Game State Logic
        private int deathCount = 0;
        private int currentLevel = 1;

        // Palette: Red must be at index 0 to survive as "last one"
        private readonly Color[] palette = new Color[]
        {
            Color.Red,          // Last survivor (Level >= 12)
            Color.OrangeRed,
            Color.DarkOrange,
            Color.Orange,
            Color.Gold,
            Color.Yellow,
            Color.YellowGreen,
            Color.LimeGreen,
            Color.Teal,
            Color.DodgerBlue,
            Color.Blue,
            Color.Indigo,
            Color.DarkViolet
        }; // 13 Colors

        public GameForm()
        {
            this.Text = "Fish Tank Game - Level Progression";
            this.ClientSize = new Size(WindowWidth, WindowHeight);
            this.DoubleBuffered = true;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            foodPanelRect = new Rectangle(0, 0, WindowWidth, FoodPanelHeight);
            fishTankRect = new Rectangle(0, FoodPanelHeight, WindowWidth, WindowHeight - FoodPanelHeight);

            fishes = new List<Fish>();

            // Timers
            gameTimer = new Timer();
            gameTimer.Interval = 16;
            gameTimer.Tick += GameLoop;

            foodTimer = new Timer();
            foodTimer.Interval = 5000;
            foodTimer.Tick += AddFood;

            // Start Level 1
            StartLevel(1);

            gameTimer.Start();
            foodTimer.Start();
        }

        private void StartLevel(int level)
        {
            currentLevel = level;
            fishes.Clear();
            deathCount = 0;
            foodCount = 0; // Fresh start for food too

            // 1. Difficulty Multipliers
            // Speed increases 20% per level (Level 1 = 1.0, Level 2 = 1.2...)
            float speedMult = 1.0f + (level - 1) * 0.2f;

            // 2. Color Logic
            // Level 1: All colors available.
            // Level increases -> Range reduces.
            // Palette has 13 colors.
            // Level 1: Range 13.
            // Level 2: Range 12.
            // ...
            // Level 13+: Range 1 (Red only).
            int availableCount = Math.Max(1, palette.Length - (level - 1));

            // Spawn 5 Fish
            Random r = new Random();
            for (int i = 0; i < 5; i++)
            {
                float x = r.Next(fishTankRect.Left, fishTankRect.Right - 30);
                float y = r.Next(fishTankRect.Top, fishTankRect.Bottom - 30);

                Color c = palette[r.Next(availableCount)];
                fishes.Add(new Fish(x, y, speedMult, c));
            }

            Invalidate();
        }

        private void AddFood(object sender, EventArgs e)
        {
            foodCount++;
            Invalidate(foodPanelRect);
        }

        private void GameLoop(object sender, EventArgs e)
        {
            foreach (var fish in fishes) fish.Move(fishTankRect);
            CheckPredation();
            Invalidate();
        }

        private void CheckPredation()
        {
            List<Fish> eatenFishes = new List<Fish>();

            for (int i = 0; i < fishes.Count; i++)
            {
                for (int j = 0; j < fishes.Count; j++)
                {
                    if (i == j) continue;

                    Fish fishA = fishes[i];
                    Fish fishB = fishes[j];

                    if (eatenFishes.Contains(fishA) || eatenFishes.Contains(fishB)) continue;

                    float dx = fishA.Center.X - fishB.Center.X;
                    float dy = fishA.Center.Y - fishB.Center.Y;
                    float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                    float radiusSum = fishA.Radius + fishB.Radius;

                    if (dist < radiusSum)
                    {
                        if (fishA.Size > fishB.Size * 1.3f)
                        {
                            eatenFishes.Add(fishB);
                        }
                    }
                }
            }

            foreach (var eaten in eatenFishes)
            {
                fishes.Remove(eaten);
                deathCount++;
            }

            // Defeat Condition
            if (deathCount >= 5)
            {
                gameTimer.Stop();
                foodTimer.Stop();
                Invalidate();
                this.Update();
                MessageBox.Show("Game Over: Too many casualties...", "Defeat", MessageBoxButtons.OK, MessageBoxIcon.Information);
                // Reset to Level 1? Or Exit? Prompt implies "Stop game".
                // We can just leave it stopped.
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 1. Draw Food Panel
            g.FillRectangle(SystemBrushes.Control, foodPanelRect);
            g.DrawLine(Pens.Black, 0, FoodPanelHeight, WindowWidth, FoodPanelHeight);

            // Draw HUD
            using (Font font = new Font("Arial", 16, FontStyle.Bold))
            {
                // Level (Left)
                g.DrawString($"Level: {currentLevel}", font, Brushes.Blue, 20, 20);

                // Alive (Center)
                string aliveText = $"Alive: {fishes.Count}";
                SizeF aliveSize = g.MeasureString(aliveText, font);
                g.DrawString(aliveText, font, Brushes.Black, (WindowWidth - aliveSize.Width) / 2, 20);

                // Death (Right)
                string deathText = $"Death: {deathCount}";
                SizeF deathSize = g.MeasureString(deathText, font);
                g.DrawString(deathText, font, Brushes.Red, WindowWidth - deathSize.Width - 20, 20);
            }

            // Draw Food Icons
            int iconSize = 20;
            int gap = 10;
            int startX = 20;
            int foodY = 60;

            for (int i = 0; i < foodCount; i++)
            {
                int x = startX + i * (iconSize + gap);
                if (x + iconSize > WindowWidth) break;
                g.FillEllipse(Brushes.Orange, x, foodY, iconSize, iconSize);
                g.DrawEllipse(Pens.DarkRed, x, foodY, iconSize, iconSize);
            }

            // 2. Tank
            using (SolidBrush waterBrush = new SolidBrush(Color.AliceBlue))
            {
                g.FillRectangle(waterBrush, fishTankRect);
            }

            foreach (var fish in fishes) fish.Draw(g);

            if (isDragging)
            {
                g.FillEllipse(Brushes.Orange, currentMousePos.X - 10, currentMousePos.Y - 10, 20, 20);
                g.DrawEllipse(Pens.DarkRed, currentMousePos.X - 10, currentMousePos.Y - 10, 20, 20);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (foodPanelRect.Contains(e.Location) && foodCount > 0)
            {
                isDragging = true;
                currentMousePos = e.Location;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (isDragging)
            {
                currentMousePos = e.Location;
                if (fishTankRect.Contains(e.Location))
                {
                    foreach (var fish in fishes) fish.TargetPosition = e.Location;
                }
                else
                {
                    foreach (var fish in fishes) fish.TargetPosition = null;
                }
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (isDragging)
            {
                isDragging = false;
                if (fishTankRect.Contains(e.Location))
                {
                    Fish closestFish = null;
                    float minDist = float.MaxValue;
                    float foodRadius = 10f;

                    foreach (var fish in fishes)
                    {
                        float dx = fish.Center.X - e.X;
                        float dy = fish.Center.Y - e.Y;
                        float dist = (float)Math.Sqrt(dx * dx + dy * dy);

                        if (dist < fish.Radius + foodRadius)
                        {
                            if (dist < minDist)
                            {
                                minDist = dist;
                                closestFish = fish;
                            }
                        }
                    }

                    if (closestFish != null)
                    {
                        // Calculate Growth Multiplier
                        float growthMult = 1.0f + (currentLevel - 1) * 0.2f;
                        closestFish.Eat(growthMult);
                        foodCount--;

                        if (closestFish.Size > 75f)
                        {
                            fishes.Add(closestFish.Clone());
                        }
                    }
                }

                foreach (var fish in fishes) fish.TargetPosition = null;

                // Victory -> Level Up
                if (fishes.Count >= 20)
                {
                    gameTimer.Stop();
                    foodTimer.Stop();
                    this.Update(); // Ensure UI updates before msg box
                    MessageBox.Show($"Level Complete! Starting Level {currentLevel + 1}", "Victory");
                    StartLevel(currentLevel + 1);
                    gameTimer.Start();
                    foodTimer.Start();
                }

                Invalidate();
            }
        }
    }
}
