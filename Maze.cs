namespace Maze
{
    public class Maze
    {
        public LevelForm Parent { get; set; } // ссылка на родительскую форму

        public Cell[,] cells;
        public static Random r = new Random();

        public Maze(LevelForm parent)
        {
            Parent = parent;
            cells = new Cell[Configuration.Rows, Configuration.Columns];
        }

        public void Generate()
        {
            foreach (PictureBox picture in Parent.Controls.OfType<PictureBox>().ToList())
            {
                Parent.Controls.Remove(picture);
                picture.Dispose();
            }

            for (ushort row = 0; row < Configuration.Rows; row++)
            {
                for (ushort col = 0; col < Configuration.Columns; col++)
                {
                    CellType cell = CellType.HALL;

                    // в 1 случае из 5 - ставим стену в текуще ячейке
                    if (r.Next(5) == 0)
                    {
                        cell = CellType.WALL;
                    }

                    // в 1 случае из 250 - кладём медаль
                    if (r.Next(100) == 0)
                    {
                        cell = CellType.MEDAL;
                    }

                    // в 1 случае из 250 - размещаем врага
                    if (r.Next(100) == 0)
                    {
                        cell = CellType.ENEMY;
                    }

                    // В 1 случае из 100 - добавляем исцеление
                    if (r.Next(100) == 0)
                    {
                        cell = CellType.HEAL;
                    }

                    // В 1 случае из 100 - добавляем коффе
                    if (r.Next(100) == 0)
                    {
                        cell = CellType.COFFEE;
                    }

                    // стены по периметру лабиринта
                    if (row == 0 || col == 0 ||
                        row == Configuration.Rows - 1 ||
                        col == Configuration.Columns - 1)
                    {
                        cell = CellType.WALL;
                    }

                    // наш персонажик
                    if (col == Parent.Hero.PosX &&
                        row == Parent.Hero.PosY)
                    {
                        cell = CellType.HERO;
                    }

                    // есть выход, и соседняя ячейка справа всегда свободна
                    if (col == Parent.Hero.PosX + 1 &&
                        row == Parent.Hero.PosY ||
                        col == Configuration.Columns - 1 &&
                        row == Configuration.Rows - 3)
                    {
                        cell = CellType.HALL;
                    }

                    cells[row, col] = new Cell(cell);

                    var picture = new PictureBox();
                    picture.Name = "pic" + row + "_" + col;
                    picture.Width = Configuration.PictureSide;
                    picture.Height = Configuration.PictureSide;
                    picture.Location = new Point(
                        col * Configuration.PictureSide,
                        row * Configuration.PictureSide);

                    picture.BackgroundImage = cells[row, col].Texture;
                    picture.Visible = false;
                    Parent.Controls.Add(picture);
                }
            }
        }

        public void Show()
        {
            for (ushort row = 0; row < Configuration.Rows; row++)
            {
                for (ushort col = 0; col < Configuration.Columns; col++)
                {
                    var picture = Parent.Controls["pic" + row + "_" + col] as PictureBox;

                    cells[row, col].Texture = Cell.Images[(int)cells[row, col].Type];
                    picture.BackgroundImage = cells[row, col].Texture;
                    picture.Visible = true;
                }
            }
        }
    }
}
