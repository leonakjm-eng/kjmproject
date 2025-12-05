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
        public float X { get; set; }
        public float Y { get; set; }
        public float DX { get; set; }
        public float DY { get; set; }
        public float Size { get; set; }
        public Color Color { get; set; }

        public int EatCount { get; set; }
        public PointF? TargetPosition { get; set; }

        public float SpeedMultiplier { get; private set; }
        public float OriginalSpeed { get; private set; }
        public float DebuffTimer { get; private set; }
        private float BaseSpeed;

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
            if (DebuffTimer > 0)
            {
                DebuffTimer -= dt;
                if (DebuffTimer < 0) DebuffTimer = 0;
            }

            float targetSpeed = (DebuffTimer > 0) ? OriginalSpeed * 0.5f : OriginalSpeed;

            float currentMag = (float)Math.Sqrt(DX * DX + DY * DY);

            if (Math.Abs(currentMag - targetSpeed) > 0.01f && currentMag > 0)
            {
                float ratio = targetSpeed / currentMag;
                DX *= ratio;
                DY *= ratio;
            }
            else if (currentMag == 0 && targetSpeed > 0)
            {
                SetVelocityVector();
            }

            BaseSpeed = targetSpeed;
        }

        public Fish Clone()
        {
            Fish newFish = new Fish(this.X, this.Y, this.SpeedMultiplier, this.Color);
            newFish.Size = this.Size;
            newFish.OriginalSpeed = this.OriginalSpeed;
            newFish.BaseSpeed = newFish.OriginalSpeed;
            newFish.SetVelocityVector();
            return newFish;
        }

        public void Move(Rectangle bounds)
        {
            if (TargetPosition.HasValue)
            {
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
            }
            else
            {
                X += DX;
                Y += DY;
            }

            // Boundary Clamping
            KeepInBounds(bounds);
        }

        public void KeepInBounds(Rectangle bounds)
        {
            // Bounce and Clamp
            if (X < bounds.Left)
            {
                X = bounds.Left;
                if (DX < 0) DX = -DX;
            }
            else if (X + Size > bounds.Right)
            {
                X = bounds.Right - Size;
                if (DX > 0) DX = -DX;
            }

            if (Y < bounds.Top)
            {
                Y = bounds.Top;
                if (DY < 0) DY = -DY;
            }
            else if (Y + Size > bounds.Bottom)
            {
                Y = bounds.Bottom - Size;
                if (DY > 0) DY = -DY;
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

        public void Eat(float growthMultiplier, FoodType type)
        {
            if (type == FoodType.Slow)
            {
                DebuffTimer = 10.0f;
            }
            else
            {
                float growthRate = 0.1f * growthMultiplier;
                Size *= (1.0f + growthRate);
                OriginalSpeed += 0.5f;
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
        private List<FoodType> foodStorage;
        private Timer gameTimer;
        private Timer foodTimer;

        private const int FoodPanelHeight = 100;
        private const int WindowWidth = 500;
        private const int WindowHeight = 500;
        private Rectangle foodPanelRect;
        private Rectangle fishTankRect;

        // Interaction
        private bool isDragging = false;
        private int draggedFoodIndex = -1;
        private FoodType draggedFoodType = FoodType.Normal;
        private Point currentMousePos;

        // Game State
        private int deathCount = 0;
        private int currentLevel = 1;

        // Auto Play & UI
        private Button btnAuto;
        private Button btnRetry;
        private bool isAutoPlay = false;
        private float autoPlayTimer = 0f;

        private readonly Color[] palette = new Color[]
        {
            Color.Red, Color.OrangeRed, Color.DarkOrange, Color.Orange,
            Color.Gold, Color.Yellow, Color.YellowGreen, Color.LimeGreen,
            Color.Teal, Color.DodgerBlue, Color.Blue, Color.Indigo, Color.DarkViolet
        };

        public GameForm()
        {
            this.Text = "Fish Tank Game - Stable Physics";
            this.ClientSize = new Size(WindowWidth, WindowHeight);
            this.DoubleBuffered = true;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            foodPanelRect = new Rectangle(0, 0, WindowWidth, FoodPanelHeight);
            fishTankRect = new Rectangle(0, FoodPanelHeight, WindowWidth, WindowHeight - FoodPanelHeight);

            // UI Controls (Moved to Top Panel Right)
            // Food Icons go up to ~370px (if gap is 5)
            btnAuto = new Button { Text = "Auto", Location = new Point(380, 60), Size = new Size(50, 25), BackColor = Color.White, Font = new Font("Arial", 8) };
            btnAuto.Click += (s, e) => {
                isAutoPlay = !isAutoPlay;
                btnAuto.BackColor = isAutoPlay ? Color.LightGreen : Color.White;
            };
            this.Controls.Add(btnAuto);

            btnRetry = new Button { Text = "Retry", Location = new Point(435, 60), Size = new Size(50, 25), BackColor = Color.White, Font = new Font("Arial", 8) };
            btnRetry.Click += (s, e) => { RestartGame(); };
            this.Controls.Add(btnRetry);

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

        private void RestartGame()
        {
            gameTimer.Stop();
            foodTimer.Stop();

            fishes.Clear();
            fallingFoods.Clear();
            foodStorage.Clear();

            deathCount = 0;
            isAutoPlay = false;
            btnAuto.BackColor = Color.White;

            StartLevel(1);

            gameTimer.Start();
            foodTimer.Start();
        }

        private int GetTargetCount()
        {
            return 6 + (currentLevel - 1);
        }

        private void StartLevel(int level)
        {
            currentLevel = level;
            // Note: Lists cleared in RestartGame or reused here if just level up
            // Ideally StartLevel clears things relevant to level start
            // But preserving foodStorage?
            // Previous logic cleared it.
            // Let's clear fishes/fallingFoods/foodStorage as per requirement "ResetGame" logic,
            // but StartLevel usually implies "Next Level" which might keep food?
            // "레벨이 바뀔 때마다 물고기 수는 다시 초기 상태... 리셋됩니다."
            // Assuming full reset of board for fairness.
            fishes.Clear();
            fallingFoods.Clear();
            foodStorage.Clear();

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
            FoodType newType = FoodType.Normal;
            if (currentLevel >= 6)
            {
                Random r = new Random();
                if (r.NextDouble() < 0.2) newType = FoodType.Slow;
            }

            if (foodStorage.Count < 15)
            {
                foodStorage.Add(newType);
            }
            else
            {
                if (currentLevel > 5)
                {
                    // Slot 15 position (index 14). Start 20, Gap 5, Size 20.
                    // X = 20 + 14 * 25 = 20 + 350 = 370.
                    float dropX = 370;
                    fallingFoods.Add(new FallingFood { X = dropX, Y = foodPanelRect.Bottom, Type = newType });
                }
            }
            Invalidate();
        }

        private void FeedFish(Fish fish, FoodType type)
        {
             float growthMult = 1.0f;
             fish.Eat(growthMult, type);

             if (fish.EatCount >= 3)
             {
                 Fish child = fish.Clone();
                 fishes.Add(child);
                 fish.EatCount = 0;
             }
        }

        private void GameLoop(object sender, EventArgs e)
        {
            float dt = 0.016f;

            foreach (var fish in fishes) fish.Update(dt);

            if (isAutoPlay)
            {
                autoPlayTimer += dt;
                if (autoPlayTimer >= 0.5f)
                {
                    autoPlayTimer = 0;
                    if (foodStorage.Count > 0 && fishes.Count > 0)
                    {
                        Fish target = null;
                        float minSize = float.MaxValue;
                        foreach(var f in fishes) {
                            if (f.Size < minSize) { minSize = f.Size; target = f; }
                        }

                        if (target != null) {
                            FoodType type = foodStorage[0];
                            foodStorage.RemoveAt(0);
                            FeedFish(target, type);
                        }
                    }
                }
            }

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
                        if (dSq < minDSq) { minDSq = dSq; closest = food; }
                    }

                    if (closest != null) fish.TargetPosition = closest.Center;
                    else fish.TargetPosition = null;
                }
            }

            foreach (var fish in fishes) fish.Move(fishTankRect);

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

            HandlePhysicsAndPredation();

            // Post-Physics Clamping (Important!)
            foreach (var fish in fishes) fish.KeepInBounds(fishTankRect);

            CheckLevelUp();
            Invalidate();
        }

        private void HandlePhysicsAndPredation()
        {
            List<Fish> eatenFishes = new List<Fish>();

            for (int i = 0; i < fishes.Count; i++)
            {
                for (int j = i + 1; j < fishes.Count; j++)
                {
                    Fish f1 = fishes[i];
                    Fish f2 = fishes[j];

                    if (eatenFishes.Contains(f1) || eatenFishes.Contains(f2)) continue;

                    float dx = f2.Center.X - f1.Center.X;
                    float dy = f2.Center.Y - f1.Center.Y;
                    float distSq = dx * dx + dy * dy;
                    float r1 = f1.Radius;
                    float r2 = f2.Radius;
                    float minDist = r1 + r2;

                    if (distSq < minDist * minDist)
                    {
                        float dist = (float)Math.Sqrt(distSq);
                        if (dist < 0.001f) dist = 0.001f;

                        bool eaten = false;
                        if (f1.Size > f2.Size * 1.3f) { eatenFishes.Add(f2); eaten = true; }
                        else if (f2.Size > f1.Size * 1.3f) { eatenFishes.Add(f1); eaten = true; }

                        if (!eaten)
                        {
                            // Physics Response (Smoothed)
                            float overlap = minDist - dist;
                            float nx = dx / dist;
                            float ny = dy / dist;

                            // Gentle Push (Smoothing)
                            float move = overlap * 0.1f; // 20% total (0.1 each)
                            f1.X -= nx * move;
                            f1.Y -= ny * move;
                            f2.X += nx * move;
                            f2.Y += ny * move;

                            // Velocity Bounce
                            float v1n = f1.DX * nx + f1.DY * ny;
                            float v2n = f2.DX * nx + f2.DY * ny;

                            float m1 = f1.Size;
                            float m2 = f2.Size;

                            float v1n_new = (v1n * (m1 - m2) + 2 * m2 * v2n) / (m1 + m2);
                            float v2n_new = (v2n * (m2 - m1) + 2 * m1 * v1n) / (m1 + m2);

                            f1.DX += (v1n_new - v1n) * nx;
                            f1.DY += (v1n_new - v1n) * ny;
                            f2.DX += (v2n_new - v2n) * nx;
                            f2.DY += (v2n_new - v2n) * ny;
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

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            g.FillRectangle(SystemBrushes.Control, foodPanelRect);
            g.DrawLine(Pens.Black, 0, FoodPanelHeight, WindowWidth, FoodPanelHeight);

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

            int iconSize = 20;
            int gap = 5; // Updated gap
            int startX = 20;
            int foodY = 60;

            for (int i = 0; i < foodStorage.Count; i++)
            {
                int x = startX + i * (iconSize + gap);
                if (x + iconSize > WindowWidth) break;

                Brush brush;
                if (foodStorage[i] == FoodType.Slow) brush = Brushes.Purple;
                else brush = Brushes.Orange;

                g.FillEllipse(brush, x, foodY, iconSize, iconSize);
                g.DrawEllipse(Pens.DarkRed, x, foodY, iconSize, iconSize);
            }

            using (SolidBrush waterBrush = new SolidBrush(Color.AliceBlue))
            {
                g.FillRectangle(waterBrush, fishTankRect);
            }

            foreach (var fish in fishes) fish.Draw(g);

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
                int iconSize = 20;
                int gap = 5; // Updated gap
                int startX = 20;
                int foodY = 60;

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

                        if (draggedFoodIndex >= 0 && draggedFoodIndex < foodStorage.Count)
                        {
                            foodStorage.RemoveAt(draggedFoodIndex);
                        }
                    }
                }

                foreach (var fish in fishes) fish.TargetPosition = null;

                draggedFoodIndex = -1;

                CheckLevelUp();
                Invalidate();
            }
        }
    }
}
