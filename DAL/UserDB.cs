using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Model;
using MySql.Data.MySqlClient;

namespace DAL
{
    /// <summary>
    /// מחלקה שאחראית על כל התקשורת והשאילתות מול טבלת ה-Users בפרויקט MuSeek.
    /// כל הפעולות משתמשות בפרמטרים למניעת הזרקות SQL.
    /// </summary>
    public class UserDB
    {
        private readonly string connectionString;

        // שליפה משותפת לכל הפעולות שמחזירות משתמש (כוללת הצטרפות לטבלת Roles ושדה IsActive)
        private const string SelectUserSql = @"
            SELECT u.UserId, u.Username, u.Email, u.PasswordHash, u.RoleId, r.RoleName, u.IsActive, u.CreatedAt
            FROM Users u
            INNER JOIN Roles r ON u.RoleId = r.RoleId";

        public UserDB(string connectionString)
        {
            this.connectionString = connectionString;
        }

        // 1. הוספת משתמש (ExecuteNonQueryAsync + LastInsertedId)
        public async Task<int> AddUserAsync(User user, string roleName = "User")
        {
            string sql = @"
                INSERT INTO Users (Username, Email, PasswordHash, RoleId, IsActive)
                SELECT @Username, @Email, @Password, RoleId, TRUE
                FROM Roles
                WHERE RoleName = @RoleName;";

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Username", user.Username);
            command.Parameters.AddWithValue("@Email", user.Email);
            command.Parameters.AddWithValue("@Password", user.Password);
            command.Parameters.AddWithValue("@RoleName", roleName);

            int rows = await command.ExecuteNonQueryAsync();

            if (rows == 0)
                throw new InvalidOperationException($"Role '{roleName}' not found.");

            user.Id = (int)command.LastInsertedId;
            return user.Id;
        }

        // 2. בדיקה אם שם משתמש תפוס (ExecuteScalarAsync)
        public async Task<bool> UsernameExistsAsync(string username)
        {
            string sql = "SELECT EXISTS(SELECT 1 FROM Users WHERE Username = @Username);";

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Username", username);

            return Convert.ToInt64(await command.ExecuteScalarAsync()) == 1;
        }

        // 3. בדיקה אם אימייל תפוס (ExecuteScalarAsync)
        public async Task<bool> EmailExistsAsync(string email)
        {
            string sql = "SELECT EXISTS(SELECT 1 FROM Users WHERE Email = @Email);";

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Email", email);

            return Convert.ToInt64(await command.ExecuteScalarAsync()) == 1;
        }

        // 4. בדיקת התחברות (ExecuteReaderAsync)
        public async Task<User?> CheckLoginAsync(string username, string password)
        {
            string sql = SelectUserSql + " WHERE u.Username = @Username AND u.IsActive = TRUE;";

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Username", username);

            using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;

            User user = ReadUser(reader);
            return user.Password == password ? user : null;
        }

        // 5. שליפת משתמש לפי מזהה (ExecuteReaderAsync)
        public async Task<User?> GetUserByIdAsync(int userId)
        {
            string sql = SelectUserSql + " WHERE u.UserId = @UserId;";

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@UserId", userId);

            using var reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync() ? ReadUser(reader) : null;
        }

        // 6. שליפת כל המשתמשים עם שם התפקיד ב-JOIN (ExecuteReaderAsync)
        public async Task<List<User>> GetAllUsersAsync()
        {
            string sql = SelectUserSql + " ORDER BY u.Username;";

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            using var command = new MySqlCommand(sql, connection);

            var users = new List<User>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                users.Add(ReadUser(reader));

            return users;
        }

        // 7. ספירת משתמשים לפי תפקיד ב-GROUP BY (ExecuteReaderAsync)
        public async Task<Dictionary<string, int>> CountUsersByRoleAsync()
        {
            string sql = @"
                SELECT r.RoleName, COUNT(u.UserId) AS UserCount
                FROM Roles r
                LEFT JOIN Users u ON u.RoleId = r.RoleId
                GROUP BY r.RoleId, r.RoleName
                ORDER BY r.RoleId;";

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            using var command = new MySqlCommand(sql, connection);

            var counts = new Dictionary<string, int>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                counts[reader.GetString(reader.GetOrdinal("RoleName"))] = Convert.ToInt32(reader["UserCount"]);

            return counts;
        }

        // 8. שינוי תפקיד של משתמש (ExecuteNonQueryAsync)
        public async Task<bool> UpdateUserRoleAsync(int userId, string roleName)
        {
            string sql = @"
                UPDATE Users u
                INNER JOIN Roles r ON r.RoleName = @RoleName
                SET u.RoleId = r.RoleId
                WHERE u.UserId = @UserId;";

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@RoleName", roleName);
            command.Parameters.AddWithValue("@UserId", userId);

            return await command.ExecuteNonQueryAsync() > 0;
        }

        // 9. עדכון סיסמה (ExecuteNonQueryAsync)
        public async Task<bool> UpdatePasswordAsync(int userId, string newPassword)
        {
            string sql = "UPDATE Users SET PasswordHash = @Password WHERE UserId = @UserId;";

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Password", newPassword);
            command.Parameters.AddWithValue("@UserId", userId);

            return await command.ExecuteNonQueryAsync() > 0;
        }

        // 10. עדכון אימייל (ExecuteNonQueryAsync)
        public async Task<bool> UpdateEmailAsync(int userId, string newEmail)
        {
            string sql = "UPDATE Users SET Email = @Email WHERE UserId = @UserId;";

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Email", newEmail);
            command.Parameters.AddWithValue("@UserId", userId);

            return await command.ExecuteNonQueryAsync() > 0;
        }

        // עדכון סטטוס פעיל/חסום - מחיקה רכה (ExecuteNonQueryAsync)
        public async Task<bool> SetUserActiveStatusAsync(int userId, bool isActive)
        {
            string sql = "UPDATE Users SET IsActive = @IsActive WHERE UserId = @UserId;";

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@IsActive", isActive);
            command.Parameters.AddWithValue("@UserId", userId);

            return await command.ExecuteNonQueryAsync() > 0;
        }

        // המרת שורה לאובייקט User
        private static User ReadUser(DbDataReader reader)
        {
            int createdAtIndex = reader.GetOrdinal("CreatedAt");

            return new User
            {
                Id = reader.GetInt32(reader.GetOrdinal("UserId")),
                Username = reader.GetString(reader.GetOrdinal("Username")),
                Email = reader.GetString(reader.GetOrdinal("Email")),
                Password = reader.GetString(reader.GetOrdinal("PasswordHash")),
                RoleId = reader.GetInt32(reader.GetOrdinal("RoleId")),
                RoleName = reader.GetString(reader.GetOrdinal("RoleName")),
                IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                CreatedAt = reader.IsDBNull(createdAtIndex) ? DateTime.MinValue : reader.GetDateTime(createdAtIndex)
            };
        }
    }
}