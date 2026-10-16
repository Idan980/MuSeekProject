using System;
using System.Data;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

namespace DAL
{
    /// <summary>
    /// מחלקה שאחראית להכין את בסיס הנתונים MuSeekDB לפני שהמערכת מתחילה לעבוד.
    /// נקראת בעליית השרת ויוצרת את הדיבי, הטבלאות ונתוני הפתיחה במידה ואינם קיימים.
    /// </summary>
    public static class DatabaseInitializer
    {
        private const string DatabaseName = "MuSeekDB";

        /// <summary>
        /// פעולת האתחול הראשית - נקראת מ-Program.cs בעת עליית האפליקציה
        /// </summary>
        public static async Task InitializeAsync(string serverConnectionString)
        {
            // שלב א': יצירת בסיס הנתונים אם אינו קיים
            await CreateDatabaseAsync(serverConnectionString);

            // מחרוזת חיבור מלאה הכוללת את שם בסיס הנתונים
            string dbConnectionString = $"{serverConnectionString}Database={DatabaseName};";

            // שלב ב': יצירת הטבלאות
            await CreateTablesAsync(dbConnectionString);

            // שלב ג': הכנסת נתוני ברירת מחדל לטבלאות
            await SeedRolesAsync(dbConnectionString);
            await SeedUsersAsync(dbConnectionString);
        }

        /// <summary>
        /// יוצרת את בסיס הנתונים MuSeekDB במידה ואינו קיים.
        /// </summary>
        private static async Task CreateDatabaseAsync(string serverConnectionString)
        {
            string sql = $"CREATE DATABASE IF NOT EXISTS `{DatabaseName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";

            using var connection = new MySqlConnection(serverConnectionString);
            await connection.OpenAsync();
            using var command = new MySqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// יוצרת את 7 הטבלאות של MuSeek כולל שדה IsActive ומפתחות זרים.
        /// </summary>
        private static async Task CreateTablesAsync(string dbConnectionString)
        {
            string createTablesSql = @"
                -- 1. טבלת תפקידים
                CREATE TABLE IF NOT EXISTS Roles (
                    RoleId INT AUTO_INCREMENT PRIMARY KEY,
                    RoleName VARCHAR(50) NOT NULL UNIQUE
                ) ENGINE=InnoDB;

                -- 2. טבלת משתמשים (כוללת מחיקה רכה - IsActive)
                CREATE TABLE IF NOT EXISTS Users (
                    UserId INT AUTO_INCREMENT PRIMARY KEY,
                    Username VARCHAR(50) NOT NULL UNIQUE,
                    Email VARCHAR(100) NOT NULL UNIQUE,
                    PasswordHash VARCHAR(255) NOT NULL,
                    RoleId INT NOT NULL,
                    IsActive BOOLEAN NOT NULL DEFAULT TRUE,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (RoleId) REFERENCES Roles(RoleId) ON DELETE RESTRICT ON UPDATE CASCADE
                ) ENGINE=InnoDB;

                -- 3. טבלת ז'אנרים
                CREATE TABLE IF NOT EXISTS Genres (
                    GenreId INT AUTO_INCREMENT PRIMARY KEY,
                    GenreName VARCHAR(50) NOT NULL UNIQUE
                ) ENGINE=InnoDB;

                -- 4. טבלת שירים
                CREATE TABLE IF NOT EXISTS Songs (
                    SongId INT AUTO_INCREMENT PRIMARY KEY,
                    Title VARCHAR(100) NOT NULL,
                    Artist VARCHAR(100) NOT NULL,
                    GenreId INT NOT NULL,
                    DurationSeconds INT,
                    FOREIGN KEY (GenreId) REFERENCES Genres(GenreId) ON DELETE RESTRICT ON UPDATE CASCADE
                ) ENGINE=InnoDB;

                -- 5. טבלת פלייליסטים (אישיים וקהילתיים)
                CREATE TABLE IF NOT EXISTS Playlists (
                    PlaylistId INT AUTO_INCREMENT PRIMARY KEY,
                    Title VARCHAR(100) NOT NULL,
                    UserId INT NOT NULL,
                    IsCommunity BOOLEAN DEFAULT FALSE,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE RESTRICT ON UPDATE CASCADE
                ) ENGINE=InnoDB;

                -- 6. טבלת איחוד: שירים בפלייליסט והצבעות
                CREATE TABLE IF NOT EXISTS PlaylistSongs (
                    PlaylistId INT NOT NULL,
                    SongId INT NOT NULL,
                    AddedByUserId INT NOT NULL,
                    VotesCount INT DEFAULT 0,
                    PRIMARY KEY (PlaylistId, SongId),
                    FOREIGN KEY (PlaylistId) REFERENCES Playlists(PlaylistId) ON DELETE CASCADE ON UPDATE CASCADE,
                    FOREIGN KEY (SongId) REFERENCES Songs(SongId) ON DELETE CASCADE ON UPDATE CASCADE,
                    FOREIGN KEY (AddedByUserId) REFERENCES Users(UserId) ON DELETE RESTRICT ON UPDATE CASCADE
                ) ENGINE=InnoDB;

                -- 7. טבלת איחוד: דירוגים ומועדפים
                CREATE TABLE IF NOT EXISTS SongRatings (
                    UserId INT NOT NULL,
                    SongId INT NOT NULL,
                    Rating INT CHECK (Rating BETWEEN 1 AND 5),
                    IsFavorite BOOLEAN DEFAULT FALSE,
                    PRIMARY KEY (UserId, SongId),
                    FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE ON UPDATE CASCADE,
                    FOREIGN KEY (SongId) REFERENCES Songs(SongId) ON DELETE CASCADE ON UPDATE CASCADE
                ) ENGINE=InnoDB;
            ";

            using var connection = new MySqlConnection(dbConnectionString);
            await connection.OpenAsync();
            using var command = new MySqlCommand(createTablesSql, connection);
            await command.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// הכנסת תפקידי הליבה של המערכת במידה והטבלה ריקה.
        /// </summary>
        private static async Task SeedRolesAsync(string dbConnectionString)
        {
            using var connection = new MySqlConnection(dbConnectionString);
            await connection.OpenAsync();

            string checkSql = "SELECT COUNT(*) FROM Roles;";
            using var checkCmd = new MySqlCommand(checkSql, connection);
            long count = Convert.ToInt64(await checkCmd.ExecuteScalarAsync());

            if (count == 0)
            {
                string insertSql = @"
                    INSERT INTO Roles (RoleId, RoleName) VALUES 
                    (1, 'SuperAdmin'),
                    (2, 'Host'),
                    (3, 'User'),
                    (4, 'Guest');
                ";
                using var insertCmd = new MySqlCommand(insertSql, connection);
                await insertCmd.ExecuteNonQueryAsync();
            }
        }

        /// <summary>
        /// הכנסת 10 משתמשים ראשוניים לצורכי בדיקות (מנהל, 2 הוסטים, 7 משתמשים)
        /// </summary>
        private static async Task SeedUsersAsync(string dbConnectionString)
        {
            using var connection = new MySqlConnection(dbConnectionString);
            await connection.OpenAsync();

            string checkSql = "SELECT COUNT(*) FROM Users;";
            using var checkCmd = new MySqlCommand(checkSql, connection);
            long count = Convert.ToInt64(await checkCmd.ExecuteScalarAsync());

            if (count == 0)
            {
                string insertSql = @"
                    INSERT INTO Users (Username, Email, PasswordHash, RoleId, IsActive) VALUES 
                    ('admin', 'admin@museek.com', '123456', 1, TRUE),
                    ('host_dan', 'dan@museek.com', '123456', 2, TRUE),
                    ('host_shira', 'shira@museek.com', '123456', 2, TRUE),
                    ('david', 'david@museek.com', '123456', 3, TRUE),
                    ('noa', 'noa@museek.com', '123456', 3, TRUE),
                    ('yossi', 'yossi@museek.com', '123456', 3, TRUE),
                    ('maya', 'maya@museek.com', '123456', 3, TRUE),
                    ('ron', 'ron@museek.com', '123456', 3, TRUE),
                    ('tal', 'tal@museek.com', '123456', 3, TRUE),
                    ('dana', 'dana@museek.com', '123456', 3, TRUE);
                ";
                using var insertCmd = new MySqlCommand(insertSql, connection);
                await insertCmd.ExecuteNonQueryAsync();
            }
        }
    }
}