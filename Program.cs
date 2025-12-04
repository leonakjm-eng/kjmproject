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

        private static Random rand = new Random();

        public Fish(float x, float y)
        {
            X = x;
            Y = y;
            Size = 30f; // Initial size 30px

            // Random velocity
            DX = (float)(rand.NextDouble() * 4 - 2); // -2 to 2
            DY = (float)(rand.NextDouble() * 4 - 2);

            // Ensure it's moving
            if (Math.Abs(DX) < 0.5f) DX = DX > 0 ? 1 : -1;
            if (Math.Abs(DY) < 0.5f) DY = DY > 0 ? 1 : -1;

            // Random Color
            Color = Color.FromArgb(rand.Next(50, 256), rand.Next(50, 256), rand.Next(50, 256));
        }

        public void Move(Rectangle bounds)
        {
            X += DX;
            Y += DY;

            // Wall Collision (Bounce)
            // Left
            if (X < bounds.Left)
            {
                X = bounds.Left;
                DX = -DX;
            }
            // Right
            else if (X + Size > bounds.Right)
            {
                X = bounds.Right - Size;
                DX = -DX;
            }
            // Top
            if (Y < bounds.Top)
            {
                Y = bounds.Top;
                DY = -DY;
            }
            // Bottom
            else if (Y + Size > bounds.Bottom)
            {
                Y = bounds.Bottom - Size;
                DY = -DY;
            }
        }

        public void Draw(Graphics g)
        {
            using (Brush b = new SolidBrush(Color))
            {
                g.FillEllipse(b, X, Y, Size, Size);
            }
            // Outline
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
        private Rectangle foodPanelRect;
        private Rectangle fishTankRect;

        // Food System
        private int foodCount = 0;

        // Interaction
        private bool isDragging = false;
        private Point currentMousePos;

        public GameForm()
        {
            // Window Settings
            this.Text = "Fish Tank Game";
            this.ClientSize = new Size(800, 600); // Fixed Resolution
            this.DoubleBuffered = true; // Prevent flickering
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            // Initialize Rectangles
            foodPanelRect = new Rectangle(0, 0, 800, FoodPanelHeight);
            fishTankRect = new Rectangle(0, FoodPanelHeight, 800, 600 - FoodPanelHeight);

            // Initialize Game State
            InitializeGame();

            // Set up Timers
            gameTimer = new Timer();
            gameTimer.Interval = 16; // Approx 60 FPS
            gameTimer.Tick += GameLoop;
            gameTimer.Start();

            foodTimer = new Timer();
            foodTimer.Interval = 10000; // 10 seconds
            foodTimer.Tick += AddFood;
            foodTimer.Start();
        }

        private void InitializeGame()
        {
            fishes = new List<Fish>();
            Random r = new Random();
            for (int i = 0; i < 5; i++)
            {
                // Ensure fish spawns inside the tank
                float x = r.Next(fishTankRect.Left, fishTankRect.Right - 30);
                float y = r.Next(fishTankRect.Top, fishTankRect.Bottom - 30);
                fishes.Add(new Fish(x, y));
            }
        }

        private void AddFood(object sender, EventArgs e)
        {
            foodCount++;
            Invalidate(foodPanelRect); // Redraw top panel
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
                this.Update(); // Force redraw before showing message box
                MessageBox.Show("Game Over: Last Survivor!", "Game Over", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 4. Redraw
            Invalidate();
        }

        private void CheckPredation()
        {
            List<Fish> eatenFishes = new List<Fish>();

            // Check every pair
            for (int i = 0; i < fishes.Count; i++)
            {
                for (int j = 0; j < fishes.Count; j++)
                {
                    if (i == j) continue;

                    Fish fishA = fishes[i];
                    Fish fishB = fishes[j];

                    // Skip if already marked as eaten
                    if (eatenFishes.Contains(fishA) || eatenFishes.Contains(fishB)) continue;

                    // Calculate Distance
                    float dx = fishA.Center.X - fishB.Center.X;
                    float dy = fishA.Center.Y - fishB.Center.Y;
                    float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                    float radiusSum = fishA.Radius + fishB.Radius;

                    // Collision Condition
                    if (dist < radiusSum)
                    {
                        // Predation Condition: SizeA > SizeB * 1.3
                        if (fishA.Size > fishB.Size * 1.3f)
                        {
                            eatenFishes.Add(fishB);
                        }
                    }
                }
            }

            // Remove eaten fish
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
            g.DrawLine(Pens.Black, 0, FoodPanelHeight, 800, FoodPanelHeight); // Divider

            // Draw available food icons
            int iconSize = 20;
            int gap = 10;
            int startX = 20;
            int startY = (FoodPanelHeight - iconSize) / 2;

            for (int i = 0; i < foodCount; i++)
            {
                // Draw up to a reasonable limit or wrap (simple row for now)
                int x = startX + i * (iconSize + gap);
                if (x + iconSize > 800) break; // clipped

                g.FillEllipse(Brushes.Orange, x, startY, iconSize, iconSize);
                g.DrawEllipse(Pens.DarkRed, x, startY, iconSize, iconSize);
            }

            // 2. Draw Fish Tank (Bottom)
            // Background is default form color, or we can make it blueish
            using (SolidBrush waterBrush = new SolidBrush(Color.AliceBlue))
            {
                g.FillRectangle(waterBrush, fishTankRect);
            }

            // Draw Fish
            foreach (var fish in fishes)
            {
                fish.Draw(g);
            }

            // 3. Draw Dragging Item
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

            // Check if clicked in Food Panel and has food
            if (foodPanelRect.Contains(e.Location) && foodCount > 0)
            {
                // Check if clicked on a food icon?
                // For better UX, let's say clicking anywhere in the food panel works if food is available.
                // Or to follow spec strictly: "FoodPanel의 먹이 아이콘 위에서"
                // Let's implement a hit test for the food row.
                int iconSize = 20;
                int gap = 10;
                int startX = 20;
                int startY = (FoodPanelHeight - iconSize) / 2;

                // Calculate width of all icons
                int totalWidth = foodCount * (iconSize + gap);
                Rectangle foodArea = new Rectangle(startX, startY, totalWidth, iconSize);

                if (foodArea.Contains(e.Location))
                {
                    isDragging = true;
                    currentMousePos = e.Location;
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (isDragging)
            {
                currentMousePos = e.Location;
                Invalidate(); // Repaint to animate drag
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
                    // Check collision with any fish
                    bool fed = false;

                    // Iterate backwards or break after first feed
                    foreach (var fish in fishes)
                    {
                        // Check if point is inside fish circle
                        // Distance check
                        float dx = e.X - fish.Center.X;
                        float dy = e.Y - fish.Center.Y;
                        float dist = (float)Math.Sqrt(dx * dx + dy * dy);

                        if (dist <= fish.Radius)
                        {
                            // Feed the fish
                            fish.Eat();
                            foodCount--;
                            fed = true;
                            break; // Feed only one fish
                        }
                    }
                }

                Invalidate();
            }
        }
    }
}
