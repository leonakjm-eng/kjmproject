using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

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
        public float Size { get; private set; } // Diameter
        public Color Color { get; private set; }

        // Feeding/Lure Logic
        public PointF? TargetPosition { get; set; }
        private float BaseSpeed;

        private static Random rand = new Random();

        public Fish(float x, float y)
        {
            X = x;
            Y = y;
            Size = 30f; // Initial size 30px

            // Random velocity (-2 to 2)
            DX = (float)(rand.NextDouble() * 4 - 2);
            DY = (float)(rand.NextDouble() * 4 - 2);

            // Ensure minimum speed
            if (Math.Abs(DX) < 0.5f) DX = DX > 0 ? 1 : -1;
            if (Math.Abs(DY) < 0.5f) DY = DY > 0 ? 1 : -1;

            // Calculate magnitude of random speed for "BaseSpeed"
            BaseSpeed = (float)Math.Sqrt(DX * DX + DY * DY);

            // Random Color
            Color = Color.FromArgb(rand.Next(50, 256), rand.Next(50, 256), rand.Next(50, 256));
        }

        public void Move(Rectangle bounds)
        {
            if (TargetPosition.HasValue)
            {
                // Lure Mode: Move towards target (cursor) at 3x speed
                float tx = TargetPosition.Value.X;
                float ty = TargetPosition.Value.Y;
                float cx = X + Size / 2;
                float cy = Y + Size / 2;

                float vecX = tx - cx;
                float vecY = ty - cy;
                float dist = (float)Math.Sqrt(vecX * vecX + vecY * vecY);

                if (dist > 1.0f) // Threshold to prevent jitter
                {
                    float speed = BaseSpeed * 3.0f;
                    float dirX = vecX / dist;
                    float dirY = vecY / dist;

                    X += dirX * speed;
                    Y += dirY * speed;
                }

                // Clamp position to bounds to prevent escaping while following cursor
                if (X < bounds.Left) X = bounds.Left;
                else if (X + Size > bounds.Right) X = bounds.Right - Size;

                if (Y < bounds.Top) Y = bounds.Top;
                else if (Y + Size > bounds.Bottom) Y = bounds.Bottom - Size;
            }
            else
            {
                // Normal Mode: Random movement with bounce
                X += DX;
                Y += DY;

                // Wall Collision (Bounce)
                if (X < bounds.Left)
                {
                    X = bounds.Left;
                    DX = -DX;
                }
                else if (X + Size > bounds.Right)
                {
                    X = bounds.Right - Size;
                    DX = -DX;
                }

                if (Y < bounds.Top)
                {
                    Y = bounds.Top;
                    DY = -DY;
                }
                else if (Y + Size > bounds.Bottom)
                {
                    Y = bounds.Bottom - Size;
                    DY = -DY;
                }
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

        public void Eat()
        {
            Size *= 1.1f; // Increase size by 10%
        }

        // Helper for collision detection
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

        public GameForm()
        {
            // Window Settings - Crowded Environment (500x500)
            this.Text = "Fish Tank Game - Lure Mode";
            this.ClientSize = new Size(WindowWidth, WindowHeight);
            this.DoubleBuffered = true;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            // Initialize Rectangles
            foodPanelRect = new Rectangle(0, 0, WindowWidth, FoodPanelHeight);
            fishTankRect = new Rectangle(0, FoodPanelHeight, WindowWidth, WindowHeight - FoodPanelHeight);

            // Initialize Game State
            InitializeGame();

            // Set up Timers
            gameTimer = new Timer();
            gameTimer.Interval = 16; // Approx 60 FPS
            gameTimer.Tick += GameLoop;
            gameTimer.Start();

            foodTimer = new Timer();
            foodTimer.Interval = 5000; // Requirement: 5 seconds
            foodTimer.Tick += AddFood;
            foodTimer.Start();
        }

        private void InitializeGame()
        {
            fishes = new List<Fish>();
            Random r = new Random();
            for (int i = 0; i < 5; i++)
            {
                float x = r.Next(fishTankRect.Left, fishTankRect.Right - 30);
                float y = r.Next(fishTankRect.Top, fishTankRect.Bottom - 30);
                fishes.Add(new Fish(x, y));
            }
        }

        private void AddFood(object sender, EventArgs e)
        {
            foodCount++;
            Invalidate(foodPanelRect);
        }

        private void GameLoop(object sender, EventArgs e)
        {
            // 1. Move Fishes
            foreach (var fish in fishes)
            {
                fish.Move(fishTankRect);
            }

            // 2. Predation Check
            CheckPredation();

            // 3. Game Over Check
            if (fishes.Count <= 1)
            {
                gameTimer.Stop();
                foodTimer.Stop();
                Invalidate();
                this.Update();
                MessageBox.Show("Game Over: Last Survivor!", "Game Over", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 4. Redraw
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
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 1. Draw Food Panel (Top)
            g.FillRectangle(SystemBrushes.Control, foodPanelRect);
            g.DrawLine(Pens.Black, 0, FoodPanelHeight, WindowWidth, FoodPanelHeight);

            // Draw available food icons
            int iconSize = 20;
            int gap = 10;
            int startX = 20;
            int startY = (FoodPanelHeight - iconSize) / 2;

            for (int i = 0; i < foodCount; i++)
            {
                int x = startX + i * (iconSize + gap);
                if (x + iconSize > WindowWidth) break;

                g.FillEllipse(Brushes.Orange, x, startY, iconSize, iconSize);
                g.DrawEllipse(Pens.DarkRed, x, startY, iconSize, iconSize);
            }

            // 2. Draw Fish Tank (Bottom)
            using (SolidBrush waterBrush = new SolidBrush(Color.AliceBlue))
            {
                g.FillRectangle(waterBrush, fishTankRect);
            }

            // Draw Fish
            foreach (var fish in fishes)
            {
                fish.Draw(g);
            }

            // 3. Draw Dragging Item (Cursor Lure)
            if (isDragging)
            {
                g.FillEllipse(Brushes.Orange, currentMousePos.X - 10, currentMousePos.Y - 10, 20, 20);
                g.DrawEllipse(Pens.DarkRed, currentMousePos.X - 10, currentMousePos.Y - 10, 20, 20);
            }
        }

        // --- Mouse Interaction ---

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

                // Lure Logic: If dragging in tank, fish swarm to cursor
                if (fishTankRect.Contains(e.Location))
                {
                    foreach (var fish in fishes)
                    {
                        fish.TargetPosition = e.Location;
                    }
                }
                else
                {
                    // If dragged back out to panel, stop luring
                    foreach (var fish in fishes)
                    {
                        fish.TargetPosition = null;
                    }
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

                // Check Drop Position
                if (fishTankRect.Contains(e.Location))
                {
                    // Instant Feed Logic
                    Fish closestFish = null;
                    float minDist = float.MaxValue;
                    float foodRadius = 10f; // Size 20

                    foreach (var fish in fishes)
                    {
                        float dx = fish.Center.X - e.X;
                        float dy = fish.Center.Y - e.Y;
                        float dist = (float)Math.Sqrt(dx * dx + dy * dy);

                        // Check collision
                        if (dist < fish.Radius + foodRadius)
                        {
                            // Find the closest one if multiple overlap
                            if (dist < minDist)
                            {
                                minDist = dist;
                                closestFish = fish;
                            }
                        }
                    }

                    if (closestFish != null)
                    {
                        closestFish.Eat();
                        foodCount--;
                    }
                }

                // Requirement: Reset ALL targets regardless of outcome
                foreach (var fish in fishes)
                {
                    fish.TargetPosition = null;
                }

                Invalidate();
            }
        }
    }
}
