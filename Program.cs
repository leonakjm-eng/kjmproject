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

    public enum FoodType { Normal, Slow }

    // Falling Food Class
    public class FallingFood
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Size { get; set; } = 20f;
        public float Speed { get; set; } = 2.0f;
        public FoodType Type { get; set; } = FoodType.Normal;

        public PointF Center => new PointF(X + Size / 2, Y + Size / 2);
        public float Radius => Size / 2;
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

        // Difficulty & Speed Properties
        public float SpeedMultiplier { get; private set; }

        // Debuff Logic
        public float OriginalSpeed { get; private set; } // Permanent Speed
        public float DebuffTimer { get; private set; } // Seconds
        private float BaseSpeed; // Current Effective Speed

        private static Random rand = new Random();

        public Fish(float x, float y, float speedMult, Color color)
        {
            X = x;
            Y = y;
            Size = 30f;
            Color = color;
            SpeedMultiplier = speedMult;
            EatCount = 0;
            DebuffTimer = 0;

            // Calculate Initial Speed
            float initSpeed = (float)(rand.NextDouble() * 2 + 1) * SpeedMultiplier;

            OriginalSpeed = initSpeed;
            BaseSpeed = OriginalSpeed;

            SetVelocityVector();
        }

        private void SetVelocityVector()
        {
            double angle = rand.NextDouble() * Math.PI * 2;
            DX = (float)(Math.Cos(angle) * BaseSpeed);
            DY = (float)(Math.Sin(angle) * BaseSpeed);
        }

        public void Update(float dt)
        {
            // Update Debuff Timer
            if (DebuffTimer > 0)
            {
                DebuffTimer -= dt;
                if (DebuffTimer < 0) DebuffTimer = 0;
            }

            // Update Effective Speed
            float targetSpeed = (DebuffTimer > 0) ? OriginalSpeed * 0.5f : OriginalSpeed;

            // Apply speed change to current velocity vector
            // Check current magnitude
            float currentMag = (float)Math.Sqrt(DX * DX + DY * DY);

            // If speed needs adjustment (e.g. state change or drift), fix it
            if (Math.Abs(currentMag - targetSpeed) > 0.01f && currentMag > 0)
            {
                float ratio = targetSpeed / currentMag;
                DX *= ratio;
                DY *= ratio;
            }
            else if (currentMag == 0 && targetSpeed > 0)
            {
                // Should not happen if moving, but safe restart
                SetVelocityVector(); // Random dir
            }

            BaseSpeed = targetSpeed;
        }

        public Fish Clone()
        {
            Fish newFish = new Fish(this.X, this.Y, this.SpeedMultiplier, this.Color);
            newFish.Size = this.Size;

            // Clone inherits Permanent Speed
            // We need to set newFish.OriginalSpeed manually because constructor randomizes it
            newFish.OriginalSpeed = this.OriginalSpeed;
            newFish.BaseSpeed = newFish.OriginalSpeed;
            newFish.SetVelocityVector(); // Apply new speed logic

            return newFish;
        }

        public void Move(Rectangle bounds)
        {
            if (TargetPosition.HasValue)
            {
                // Chase Mode: Move towards target at 3x speed
                // BaseSpeed is already adjusted by Debuff
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

            // Optional: Indicate Slow State visual?
            // Prompt says: "Color 속성을 건드리는 코드는 넣지 마세요."
            // But we can draw an icon or text.
            if (DebuffTimer > 0)
            {
                 g.DrawString("Slow", SystemFonts.DefaultFont, Brushes.Black, X, Y - 15);
            }
        }

        public void Eat(float growthMultiplier, FoodType type)
        {
            if (type == FoodType.Slow)
            {
                // Slow Food: Debuff only
                DebuffTimer = 10.0f; // 10 Seconds
                // Speed calculation handled in Update()
            }
            else
            {
                // Normal Food: Growth & Speed Up
                float growthRate = 0.1f * growthMultiplier;
                Size *= (1.0f + growthRate);

                // Permanent Speed Up
                OriginalSpeed += 0.5f;
                // BaseSpeed updated in next Update()

                EatCount++;
            }
        }

        public PointF Center => new PointF(X + Size / 2, Y + Size / 2);
        public float Radius => Size / 2;
    }

    // Main Game Form
    public class GameForm : Form
    {
        private List<Fish> fishes;
        private List<FallingFood> fallingFoods;
        private List<FoodType> foodStorage; // UI Food Storage
        private Timer gameTimer;
        private Timer foodTimer;

        private const int FoodPanelHeight = 100;
        private const int WindowWidth = 500;
        private const int WindowHeight = 500;
        private Rectangle foodPanelRect;
        private Rectangle fishTankRect;

        // Interaction
        private bool isDragging = false;
        private int draggedFoodIndex = -1; // Which UI food is being dragged
        private FoodType draggedFoodType = FoodType.Normal;
        private Point currentMousePos;

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
            this.Text = "Fish Tank Game - Slow Food Strategy";
            this.ClientSize = new Size(WindowWidth, WindowHeight);
            this.DoubleBuffered = true;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            foodPanelRect = new Rectangle(0, 0, WindowWidth, FoodPanelHeight);
            fishTankRect = new Rectangle(0, FoodPanelHeight, WindowWidth, WindowHeight - FoodPanelHeight);

            fishes = new List<Fish>();
            fallingFoods = new List<FallingFood>();
            foodStorage = new List<FoodType>();

            gameTimer = new Timer();
            gameTimer.Interval = 16;
            gameTimer.Tick += GameLoop;

            foodTimer = new Timer();
            foodTimer.Interval = 2000;
            foodTimer.Tick += AddFood;

            StartLevel(1);

            gameTimer.Start();
            foodTimer.Start();
        }

        private int GetTargetCount()
        {
            return 6 + (currentLevel - 1) * 2;
        }

        private void StartLevel(int level)
        {
            currentLevel = level;
            fishes.Clear();
            fallingFoods.Clear();
            foodStorage.Clear();
            deathCount = 0;

            float speedMult = 1.0f + (level - 1) * 0.2f;
            int availableCount = Math.Max(1, palette.Length - (level - 1));
            int initialCount = 3 + (level - 1) / 2;

            Random r = new Random();
            for (int i = 0; i < initialCount; i++)
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
            // Determine Type
            FoodType newType = FoodType.Normal;
            if (currentLevel >= 6)
            {
                Random r = new Random();
                if (r.NextDouble() < 0.2) // 20%
                {
                    newType = FoodType.Slow;
                }
            }

            if (foodStorage.Count < 15)
            {
                foodStorage.Add(newType);
            }
            else
            {
                // Overflow: Drop from 15th slot
                float dropX = 20 + 14 * 30;
                fallingFoods.Add(new FallingFood { X = dropX, Y = foodPanelRect.Bottom, Type = newType });
            }
            Invalidate();
        }

        private void FeedFish(Fish fish, FoodType type)
        {
             float growthMult = 1.0f + (currentLevel - 1) * 0.2f;
             fish.Eat(growthMult, type);

             // Only reproduce if Normal food increased count
             if (fish.EatCount >= 3)
             {
                 Fish child = fish.Clone();
                 fishes.Add(child);
                 fish.EatCount = 0;
             }
        }

        private void GameLoop(object sender, EventArgs e)
        {
            float dt = 0.016f; // Approx dt for 60fps

            // Update Fish Logic
            foreach (var fish in fishes) fish.Update(dt);

            // 1. AI Logic: Set Targets
            if (isDragging)
            {
                if (fishTankRect.Contains(currentMousePos))
                {
                    foreach (var fish in fishes) fish.TargetPosition = currentMousePos;
                }
            }
            else
            {
                foreach (var fish in fishes)
                {
                    FallingFood closest = null;
                    float minDSq = float.MaxValue;

                    foreach(var food in fallingFoods)
                    {
                        float dx = fish.Center.X - food.Center.X;
                        float dy = fish.Center.Y - food.Center.Y;
                        float dSq = dx * dx + dy * dy;

                        if (dSq < minDSq)
                        {
                            minDSq = dSq;
                            closest = food;
                        }
                    }

                    if (closest != null)
                    {
                        fish.TargetPosition = closest.Center;
                    }
                    else
                    {
                        fish.TargetPosition = null;
                    }
                }
            }

            // 2. Move Fishes
            foreach (var fish in fishes) fish.Move(fishTankRect);

            // 3. Move Falling Foods & Collision
            for (int i = fallingFoods.Count - 1; i >= 0; i--)
            {
                var food = fallingFoods[i];
                food.Y += food.Speed;

                Fish eater = null;
                foreach(var fish in fishes)
                {
                    float dx = fish.Center.X - food.Center.X;
                    float dy = fish.Center.Y - food.Center.Y;
                    float dist = (float)Math.Sqrt(dx * dx + dy * dy);

                    if (dist < fish.Radius + food.Radius)
                    {
                        eater = fish;
                        break;
                    }
                }

                if (eater != null)
                {
                    FeedFish(eater, food.Type);
                    fallingFoods.RemoveAt(i);
                }
                else if (food.Y > fishTankRect.Bottom)
                {
                    fallingFoods.RemoveAt(i);
                }
            }

            CheckPredation();
            CheckLevelUp();
            Invalidate();
        }

        private void CheckLevelUp()
        {
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
                g.DrawString($"Level: {currentLevel}", font, Brushes.Blue, 20, 20);

                string aliveText = $"Alive: {fishes.Count} / {GetTargetCount()}";
                SizeF aliveSize = g.MeasureString(aliveText, font);
                g.DrawString(aliveText, font, Brushes.Black, (WindowWidth - aliveSize.Width) / 2, 20);

                string deathText = $"Death: {deathCount}";
                SizeF deathSize = g.MeasureString(deathText, font);
                g.DrawString(deathText, font, Brushes.Red, WindowWidth - deathSize.Width - 20, 20);
            }

            // Draw Food Icons
            int iconSize = 20;
            int gap = 10;
            int startX = 20;
            int foodY = 60;

            // Draw based on foodStorage list
            for (int i = 0; i < foodStorage.Count; i++)
            {
                int x = startX + i * (iconSize + gap);
                if (x + iconSize > WindowWidth) break;

                Brush brush;
                if (foodStorage[i] == FoodType.Slow) brush = Brushes.Purple; // Slow Food Color
                else brush = Brushes.Orange;

                g.FillEllipse(brush, x, foodY, iconSize, iconSize);
                g.DrawEllipse(Pens.DarkRed, x, foodY, iconSize, iconSize);
            }

            // 2. Tank
            using (SolidBrush waterBrush = new SolidBrush(Color.AliceBlue))
            {
                g.FillRectangle(waterBrush, fishTankRect);
            }

            // Draw Fish
            foreach (var fish in fishes) fish.Draw(g);

            // Draw Falling Food
            foreach (var food in fallingFoods)
            {
                Brush brush;
                if (food.Type == FoodType.Slow) brush = Brushes.Purple;
                else brush = Brushes.OrangeRed;

                g.FillEllipse(brush, food.X, food.Y, food.Size, food.Size);
                g.DrawEllipse(Pens.DarkRed, food.X, food.Y, food.Size, food.Size);
            }

            if (isDragging)
            {
                // Draw Dragged Food
                Brush brush;
                if (draggedFoodType == FoodType.Slow) brush = Brushes.Purple;
                else brush = Brushes.Orange;

                g.FillEllipse(brush, currentMousePos.X - 10, currentMousePos.Y - 10, 20, 20);
                g.DrawEllipse(Pens.DarkRed, currentMousePos.X - 10, currentMousePos.Y - 10, 20, 20);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (foodPanelRect.Contains(e.Location) && foodStorage.Count > 0)
            {
                // Identify which food is clicked
                int iconSize = 20;
                int gap = 10;
                int startX = 20;
                int foodY = 60;

                // Simple hit test on slots
                for (int i = 0; i < foodStorage.Count; i++)
                {
                    int x = startX + i * (iconSize + gap);
                    Rectangle iconRect = new Rectangle(x, foodY, iconSize, iconSize);

                    if (iconRect.Contains(e.Location))
                    {
                        isDragging = true;
                        draggedFoodIndex = i;
                        draggedFoodType = foodStorage[i];
                        currentMousePos = e.Location;
                        break;
                    }
                }

                // If not precise hit, maybe drag the last one?
                // Previous logic was "click anywhere in panel".
                // Let's fallback to "last one" if clicked loosely in panel but not on specific icon?
                // Or sticky to precision. Precision is better for strategy ("Use Slow Food").
                // If the user misses the icon, nothing happens.
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (isDragging)
            {
                currentMousePos = e.Location;
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
                        FeedFish(closestFish, draggedFoodType);

                        // Remove the specific food
                        if (draggedFoodIndex >= 0 && draggedFoodIndex < foodStorage.Count)
                        {
                            foodStorage.RemoveAt(draggedFoodIndex);
                        }
                    }
                }

                foreach (var fish in fishes) fish.TargetPosition = null;

                // Reset drag state
                draggedFoodIndex = -1;

                CheckLevelUp();
                Invalidate();
            }
        }
    }
}
