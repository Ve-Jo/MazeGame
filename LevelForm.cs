using System;
using System.ComponentModel;
using System.Globalization;
using System.Media;
using System.Windows.Forms;

namespace Maze
{
    public partial class LevelForm : Form
    {
        private CultureInfo currentCulture;

        public Maze maze;
        public Character Hero;
        public Random random;

        int medalCount;
        int totalMedals;
        int health;
        int energy;
        int energyUsageCooldown;

        int movementSteps = 0;
        int totalMovementSteps = 0;

        private DateTime gameStartTime;
        private bool bombPlaced = false;
        private List<Point> placedBombs = new List<Point>();

        SoundPlayer gun = new SoundPlayer(Properties.Resources.gun);
        SoundPlayer hit = new SoundPlayer(Properties.Resources.hit);
        SoundPlayer background = new SoundPlayer(Properties.Resources.background);
        SoundPlayer coffee_s = new SoundPlayer(Properties.Resources.coffee1);
        SoundPlayer medal_s = new SoundPlayer(Properties.Resources.medal1);
        SoundPlayer enemy_hit = new SoundPlayer(Properties.Resources.enemy_hit);
        SoundPlayer bomb_placed = new SoundPlayer(Properties.Resources.bomb_placed);
        SoundPlayer bomb_explosion = new SoundPlayer(Properties.Resources.bomb_explosion);
        SoundPlayer win = new SoundPlayer(Properties.Resources.win);
        SoundPlayer fail = new SoundPlayer(Properties.Resources.fail);
        SoundPlayer healing = new SoundPlayer(Properties.Resources.healing);

        private Direction lastTravelDirection;
        public enum Direction
        {
            Up,
            Down,
            Left,
            Right
        }

        public LevelForm()
        {
            InitializeComponent();
            InitializeLocalization();
            ConfigureForm();
            StartGame();
            random = new Random();
        }

        private void InitializeLocalization()
        {
            currentCulture = CultureInfo.DefaultThreadCurrentUICulture;

            ToolStripButton englishButton = new ToolStripButton();
            englishButton.Image = Properties.Resources.english_flag; // Replace with actual image
            englishButton.Click += (sender, e) => ChangeLanguage(CultureInfo.GetCultureInfo("en-US"));

            ToolStripButton ukrainianButton = new ToolStripButton();
            ukrainianButton.Image = Properties.Resources.ukraine_flag; // Replace with actual image
            ukrainianButton.Click += (sender, e) => ChangeLanguage(CultureInfo.GetCultureInfo("uk-UA"));

            ToolStripButton polishButton = new ToolStripButton();
            polishButton.Image = Properties.Resources.poland_flag; // Replace with actual image
            polishButton.Click += (sender, e) => ChangeLanguage(CultureInfo.GetCultureInfo("pl-PL"));

            statusStrip1.Items.Add(englishButton);
            statusStrip1.Items.Add(ukrainianButton);
            statusStrip1.Items.Add(polishButton);
        }


        private void ChangeLanguage(CultureInfo culture)
        {
            if (currentCulture != culture)
            {
                currentCulture = culture;

                // Check if the selected culture is Ukrainian
                if (culture.Name == "uk-UA")
                {
                    // If Ukrainian is selected, set the CurrentUICulture to InvariantCulture
                    Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
                }
                else
                {
                    // Otherwise, set the CurrentUICulture to the selected culture
                    Thread.CurrentThread.CurrentUICulture = culture;
                }

                ComponentResourceManager resources = new ComponentResourceManager(typeof(LevelForm));
                resources.ApplyResources(this, "$this");
                ApplyResources(resources, Controls);
            }
        }

        private void ApplyResources(ComponentResourceManager resources, Control.ControlCollection controls)
        {
            foreach (Control control in controls)
            {
                resources.ApplyResources(control, control.Name);
                ApplyResources(resources, control.Controls);
            }
        }

        public void ConfigureForm()
        {
            Text = Properties.Resources.Title;
            BackColor = Configuration.Background;
            ClientSize = new Size(Configuration.Columns * Configuration.PictureSide, Configuration.Rows * Configuration.PictureSide);
            StartPosition = FormStartPosition.CenterScreen;
        }

        public void ResetVariables()
        {
            medalCount = 0;
            totalMedals = 0;
            health = 100;
            energy = 500;
            energyUsageCooldown = 0;
        }

        public void StartGame()
        {
            Hero = new Character(this);
            maze = new Maze(this);
            maze.Generate();
            maze.Show();
            ResetVariables();
            CalculateTotalMedals();
            UpdateLabel();
            gameStartTime = DateTime.Now;
            background.PlayLooping();
        }

        public void CalculateTotalMedals()
        {
            totalMedals = maze.cells.Cast<Cell>().Count(cell => cell.Type == CellType.MEDAL);
        }

        public void UpdateHeroPosition(ushort targetX, ushort targetY)
        {
            Hero.Clear();
            Hero.PosX = targetX;
            Hero.PosY = targetY;
            Hero.Show();
        }

        public void UseShiftAttack()
        {
            for (ushort row = 0; row < Configuration.Rows; row++)
            {
                for (ushort col = 0; col < Configuration.Columns; col++)
                {
                    if (maze.cells[row, col].Type == CellType.ENEMY)
                    {
                        int distance = Math.Abs(row - Hero.PosY) + Math.Abs(col - Hero.PosX);
                        if (distance <= 8)
                        {
                            maze.cells[row, col].Type = CellType.HALL;
                            enemy_hit.Play();
                            HandleAllEnemiesDead();
                        }
                    }
                }
            }
            energy -= 10;
            UpdateLabel();
            maze.Show();
        }

        private async Task ShootBullet()
        {
            ushort bulletX = Hero.PosX;
            ushort bulletY = Hero.PosY;

            gun.Play();

            List<(ushort, ushort)> bulletPath = new List<(ushort, ushort)>();

            while (true)
            {
                switch (lastTravelDirection)
                {
                    case Direction.Up:
                        bulletY--;
                        break;
                    case Direction.Down:
                        bulletY++;
                        break;
                    case Direction.Left:
                        bulletX--;
                        break;
                    case Direction.Right:
                        bulletX++;
                        break;
                }

                if (bulletX >= Configuration.Columns || bulletY >= Configuration.Rows || maze.cells[bulletY, bulletX].Type == CellType.WALL)
                {
                    break;
                }

                bulletPath.Add((bulletY, bulletX));

                if (maze.cells[bulletY, bulletX].Type == CellType.ENEMY)
                {
                    maze.cells[bulletY, bulletX].Type = CellType.HALL;
                    enemy_hit.Play();
                    HandleAllEnemiesDead();
                    break;
                }
            }

            foreach (var (row, col) in bulletPath)
            {
                maze.cells[row, col].Type = CellType.BULLET;
                maze.Show();
                await Task.Delay(5);
            }

            foreach (var (row, col) in bulletPath)
            {
                maze.cells[row, col].Type = CellType.HALL;
                maze.Show();
                await Task.Delay(50);
            }
        }


        private void PlaceBomb()
        {
            if (energy >= 49)
            {
                energy -= 49;
                int bombY = Hero.PosY;
                int bombX = Hero.PosX;
                MovePlayerToAdjacentHall();

                bomb_placed.Play();

                maze.cells[bombY, bombX].Type = CellType.BOMB;
                placedBombs.Add(new Point(Hero.PosX, Hero.PosY));

                MessageBox.Show(Properties.Resources.BombPlanted);

                UpdateLabel();
                maze.Show();
            }
        }

        private void DetonateBombs()
        {
            foreach (Point bomb in placedBombs)
            {
                bomb_explosion.Play();

                int bombX = bomb.X;
                int bombY = bomb.Y;

                for (int offsetY = -3; offsetY <= 3; offsetY++)
                {
                    for (int offsetX = -3; offsetX <= 3; offsetX++)
                    {
                        int targetX = bombX + offsetX;
                        int targetY = bombY + offsetY;

                        if (targetX >= 0 && targetX < Configuration.Columns &&
                            targetY >= 0 && targetY < Configuration.Rows)
                        {
                            maze.cells[targetY, targetX].Type = CellType.HALL;

                            if (targetX == Hero.PosX && targetY == Hero.PosY)
                            {
                                fail.Play();
                                MessageBox.Show(Properties.Resources.BombDied, Properties.Resources.Defeat, MessageBoxButtons.OK, MessageBoxIcon.Information);
                                health = 0;
                                UpdateLabel();
                                Hero.Clear();
                                StartGame();
                            }
                        }
                    }
                }
            }

            placedBombs.Clear();
            HandleAllEnemiesDead();
            UpdateLabel();
            maze.Show();
        }


        private void MovePlayerToAdjacentHall()
        {
            if (Hero.PosX + 1 < Configuration.Columns && maze.cells[Hero.PosY, Hero.PosX + 1].Type == CellType.HALL)
            {
                UpdateHeroPosition((ushort)(Hero.PosX + 1), Hero.PosY);
            }
            else if (Hero.PosX - 1 >= 0 && maze.cells[Hero.PosY, Hero.PosX - 1].Type == CellType.HALL)
            {
                UpdateHeroPosition((ushort)(Hero.PosX - 1), Hero.PosY);
            }
            else if (Hero.PosY + 1 < Configuration.Rows && maze.cells[Hero.PosY + 1, Hero.PosX].Type == CellType.HALL)
            {
                UpdateHeroPosition(Hero.PosX, (ushort)(Hero.PosY + 1));
            }
            else if (Hero.PosY - 1 >= 0 && maze.cells[Hero.PosY - 1, Hero.PosX].Type == CellType.HALL)
            {
                UpdateHeroPosition(Hero.PosX, (ushort)(Hero.PosY - 1));
            }
        }

        public void UpdateLabel()
        {
            if (health < 0 || energy < 0)
            {
                ShowDefeatMessageBox();
            }
            else
            {
                UpdateStatusLabel();
            }
        }

        public void ShowDefeatMessageBox()
        {
            DialogResult result = MessageBox.Show(Properties.Resources.YouDied, Properties.Resources.Defeat, MessageBoxButtons.OK, MessageBoxIcon.Information);
            fail.Play();
            if (result == DialogResult.OK)
            {
                Hero.Clear();
                StartGame();
                UpdateLabel();
            }
        }

        public void UpdateStatusLabel()
        {
            Text = Properties.Resources.Title.Replace("{medals}", medalCount.ToString())
                                       .Replace("{health}", health.ToString())
                                       .Replace("{energy}", energy.ToString());

            toolStripStatusLabel1.Text = string.Format(Properties.Resources.StatusLabelHealth, health);

            TimeSpan elapsedTime = DateTime.Now - gameStartTime;
            toolStripStatusLabel2.Text = string.Format(Properties.Resources.StatusLabelTime, elapsedTime.ToString(@"mm\:ss"));

            toolStripStatusLabel3.Text = string.Format(Properties.Resources.StatusLabelSteps, totalMovementSteps);
        }

        public void HandleRegularMovement(ushort targetX, ushort targetY)
        {
            energy--;
            if (energyUsageCooldown > 0) { energyUsageCooldown--; }

            if (targetX >= Configuration.Columns || targetY >= Configuration.Rows)
                return;

            CellType targetType = maze.cells[targetY, targetX].Type;

            if (targetType == CellType.BOMB)
            {
                fail.Play();
                MessageBox.Show(Properties.Resources.BombDied, Properties.Resources.Defeat, MessageBoxButtons.OK, MessageBoxIcon.Information);
                health = 0;
                UpdateLabel();
                Hero.Clear();
                StartGame();
            }
            else if (targetType == CellType.ENEMY)
            {
                HandleEnemyCollision(targetX, targetY);
            }
            else if (targetType == CellType.MEDAL)
            {
                HandleMedalCollection(targetX, targetY);
            }
            else if (targetType == CellType.COFFEE)
            {
                HandleCoffeePickup(targetX, targetY);
            }
            else if (targetType == CellType.HEAL)
            {
                HandleHealPickup(targetX, targetY);
            }
            else if (targetType == CellType.WALL)
            {
                return;
            }
            else
            {
                UpdateHeroPosition(targetX, targetY);
                UpdateLabel();

                if (Hero.PosX >= Configuration.Columns - 1)
                {
                    HandleVictory();
                }
            }
        }

        public void HandleEnemyCollision(ushort targetX, ushort targetY)
        {
            health -= random.Next(20, 25);
            hit.Play();
            UpdateLabel();
            if (health < 0)
            {
                health = 0;
                UpdateLabel();
                Hero.Clear();
                StartGame();
            }
            else
            {
                UpdateHeroPosition(targetX, targetY);
                HandleAllEnemiesDead();
            }
        }

        public void HandleMedalCollection(ushort targetX, ushort targetY)
        {
            medalCount++;
            medal_s.Play();
            UpdateHeroPosition(targetX, targetY);
            UpdateLabel();
            if (medalCount == totalMedals)
            {
                win.Play();
                MessageBox.Show(Properties.Resources.MedalsAll, Properties.Resources.Collector, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

        }

        public void HandleAllEnemiesDead()
        {
            // Check if all enemy cells are of type CellType.HALL
            bool allEnemiesDead = true;
            for (ushort row = 0; row < Configuration.Rows; row++)
            {
                for (ushort col = 0; col < Configuration.Columns; col++)
                {
                    if (maze.cells[row, col].Type == CellType.ENEMY)
                    {
                        allEnemiesDead = false;
                        break;
                    }
                }
                if (!allEnemiesDead) break;
            }

            if (allEnemiesDead)
            {
                win.Play();
                MessageBox.Show(Properties.Resources.EnemiesAll, Properties.Resources.YouWin, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        public void HandleCoffeePickup(ushort targetX, ushort targetY)
        {
            if (energyUsageCooldown > 0)
            {
                MessageBox.Show(Properties.Resources.CoffeMuch, Properties.Resources.Warning, MessageBoxButtons.OK, MessageBoxIcon.Information);
                energyUsageCooldown++;
            }
            else
            {
                coffee_s.Play();
                energy += 25;
                energyUsageCooldown = 10;
                UpdateHeroPosition(targetX, targetY);
                UpdateLabel();
            }
        }

        public void HandleHealPickup(ushort targetX, ushort targetY)
        {
            if (health + 5 >= 100)
            {
                MessageBox.Show(Properties.Resources.PeelsMuch, Properties.Resources.Warning, MessageBoxButtons.OK, MessageBoxIcon.Information);
                maze.cells[targetY, targetX].Type = CellType.HEAL;
            }
            else
            {
                healing.Play();
                health += 5;
                UpdateHeroPosition(targetX, targetY);
                UpdateLabel();
            }
        }

        public void HandleVictory()
        {
            win.Play();
            DialogResult result = MessageBox.Show(Properties.Resources.YouWin, Properties.Resources.Congrats, MessageBoxButtons.OK, MessageBoxIcon.Information);
            if (result == DialogResult.OK)
            {
                Hero.Clear();
                StartGame();
            }
        }

        private void SpawnEnemyIfNeeded()
        {
            if (movementSteps >= 20)
            {
                movementSteps = 0;
                ushort emptyRow, emptyCol;
                do
                {
                    emptyRow = (ushort)random.Next(Configuration.Rows);
                    emptyCol = (ushort)random.Next(Configuration.Columns);
                } while (maze.cells[emptyRow, emptyCol].Type != CellType.HALL);

                maze.cells[emptyRow, emptyCol].Type = CellType.ENEMY;
                maze.Show();
            }
        }

        private async void KeyDownHandler(object sender, KeyEventArgs e)
        {
            ushort targetX = Hero.PosX;
            ushort targetY = Hero.PosY;

            if (e.KeyCode == Keys.Tab)
            {
                if (energy >= 20)
                {
                    energy -= 20;
                    await ShootBullet();
                    UpdateLabel();
                }
                return;
            }
            else if (e.KeyCode == Keys.ShiftKey)
            {
                if (energy >= 10)
                {
                    UseShiftAttack();
                }
                return;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                PlaceBomb();
                return;
            }
            else if (e.KeyCode == Keys.Space)
            {
                DetonateBombs();
                return;
            }

            // Handle arrow key movement steps incrementing
            if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Left || e.KeyCode == Keys.Up || e.KeyCode == Keys.Down)
            {
                movementSteps++;
                totalMovementSteps++;
            }

            // Set lastTravelDirection based on arrow keys
            if (e.KeyCode == Keys.Right)
            {
                lastTravelDirection = Direction.Right;
                targetX = (ushort)(Hero.PosX + 1);
            }
            else if (e.KeyCode == Keys.Left)
            {
                lastTravelDirection = Direction.Left;
                targetX = (ushort)(Hero.PosX - 1);
            }
            else if (e.KeyCode == Keys.Up)
            {
                lastTravelDirection = Direction.Up;
                targetY = (ushort)(Hero.PosY - 1);
            }
            else if (e.KeyCode == Keys.Down)
            {
                lastTravelDirection = Direction.Down;
                targetY = (ushort)(Hero.PosY + 1);
            }

            HandleRegularMovement(targetX, targetY);
            SpawnEnemyIfNeeded();
        }
    }
}