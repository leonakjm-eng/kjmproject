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

        // Logic Properties
        public int EatCount { get; set; }
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
            EatCount = 0;

            // Random velocity scaled by multiplier
            // Initial base speed is around 1.0 * SpeedMultiplier (vector length ~1.4 to 2.8?)
            // Let's generate random vector and normalize to a specific speed range.
            float initSpeed = (float)(rand.NextDouble() * 2 + 1) * SpeedMultiplier; // 1 to 3 * mult
            SetVelocity(initSpeed);
        }

        private void SetVelocity(float speed)
        {
            double angle = rand.NextDouble() * Math.PI * 2;
            DX = (float)(Math.Cos(angle) * speed);
            DY = (float)(Math.Sin(angle) * speed);
            BaseSpeed = speed;
        }

        // Clone method for Reproduction
        public Fish Clone()
        {
            Fish newFish = new Fish(this.X, this.Y, this.SpeedMultiplier, this.Color);

            // Inherit traits
            newFish.Size = this.Size;
            // Inherit Speed (BaseSpeed) but random direction
            newFish.SetVelocity(this.BaseSpeed);

            // EatCount is 0 from constructor
            return newFish;
        }

        public void Move(Rectangle bounds)
        {
            if (TargetPosition.HasValue)
            {
                // Lure Mode: Move towards target at 3x speed
                // Speed increases as fish eats
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
            // 1. Growth
            float growthRate = 0.1f * growthMultiplier;
            Size *= (1.0f + growthRate);

            // 2. Speed Up (Permanent)
            // Increase magnitude by 0.5f
            float newSpeed = BaseSpeed + 0.5f;

            // Update velocity vector magnitude
            if (BaseSpeed > 0)
            {
                float ratio = newSpeed / BaseSpeed;
                DX *= ratio;
                DY *= ratio;
            }
            BaseSpeed = newSpeed;

            // 3. Increment Count
            EatCount++;
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

        private readonly Color[] palette = new Color[]
        {
            Color.Red, Color.OrangeRed, Color.DarkOrange, Color.Orange,
            Color.Gold, Color.Yellow, Color.YellowGreen, Color.LimeGreen,
            Color.Teal, Color.DodgerBlue, Color.Blue, Color.Indigo, Color.DarkViolet
        };

        public GameForm()
        {
            this.Text = "Fish Tank Game - Dynamic Balance";
            this.ClientSize = new Size(WindowWidth, WindowHeight);
            this.DoubleBuffered = true;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            foodPanelRect = new Rectangle(0, 0, WindowWidth, FoodPanelHeight);
            fishTankRect = new Rectangle(0, FoodPanelHeight, WindowWidth, WindowHeight - FoodPanelHeight);

            fishes = new List<Fish>();

            gameTimer = new Timer();
            gameTimer.Interval = 16;
            gameTimer.Tick += GameLoop;

            foodTimer = new Timer();
            foodTimer.Interval = 5000;
            foodTimer.Tick += AddFood;

            StartLevel(1);

            gameTimer.Start();
            foodTimer.Start();
        }

        private int GetTargetCount()
        {
            // Level 1: 10, Level 2: 12 ...
            return 10 + (currentLevel - 1) * 2;
        }

        private void StartLevel(int level)
        {
            currentLevel = level;
            fishes.Clear();
            deathCount = 0;
            foodCount = 0;

            float speedMult = 1.0f + (level - 1) * 0.2f;
            int availableCount = Math.Max(1, palette.Length - (level - 1));

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

            // Defeat
            if (deathCount >= 5)
            {
                gameTimer.Stop();
                foodTimer.Stop();
                Invalidate();
                this.Update();
                MessageBox.Show("Game Over: Too many casualties...", "Defeat", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

                // Alive / Target (Center)
                string aliveText = $"Alive: {fishes.Count} / {GetTargetCount()}";
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
                        float growthMult = 1.0f + (currentLevel - 1) * 0.2f;
                        closestFish.Eat(growthMult);
                        foodCount--;

                        // Reproduction Logic (Count >= 3)
                        if (closestFish.EatCount >= 3)
                        {
                            Fish child = closestFish.Clone();
                            fishes.Add(child);

                            // Reset counters
                            closestFish.EatCount = 0;
                            // child.EatCount is 0 by default/clone
                        }
                    }
                }

                foreach (var fish in fishes) fish.TargetPosition = null;

                // Level Up Check
                if (fishes.Count >= GetTargetCount())
                {
                    gameTimer.Stop();
                    foodTimer.Stop();
                    this.Update();
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
