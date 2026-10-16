using DAL;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Model;

namespace API.Controllers
{
    /// <summary>
    /// בקר נקודות קצה (API) לניהול משתמשים בפרויקט MuSeek.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly UserDB userDB;

        public UsersController(UserDB userDB)
        {
            this.userDB = userDB;
        }

        // שליפת כל המשתמשים
        [HttpGet]
        public async Task<ActionResult<List<User>>> GetAll()
        {
            return Ok(await userDB.GetAllUsersAsync());
        }

        // שליפת משתמש לפי מזהה (Id)
        [HttpGet("{id:int}")]
        public async Task<ActionResult<User>> GetById(int id)
        {
            User? user = await userDB.GetUserByIdAsync(id);
            return user == null ? NotFound() : Ok(user);
        }

        // בדיקה אם שם משתמש תפוס
        [HttpGet("exists/{username}")]
        public async Task<ActionResult<bool>> UsernameExists(string username)
        {
            return Ok(await userDB.UsernameExistsAsync(username));
        }

        // בדיקה אם אימייל תפוס
        [HttpGet("email-exists/{email}")]
        public async Task<ActionResult<bool>> EmailExists(string email)
        {
            return Ok(await userDB.EmailExistsAsync(email));
        }

        // ספירת המשתמשים לפי תפקיד
        [HttpGet("count-by-role")]
        public async Task<ActionResult<Dictionary<string, int>>> CountByRole()
        {
            return Ok(await userDB.CountUsersByRoleAsync());
        }

        // הוספת משתמש חדש (ברירת מחדל לתפקיד: User)
        [HttpPost]
        public async Task<ActionResult<User>> Add([FromBody] User user, [FromQuery] string roleName = "User")
        {
            if (await userDB.UsernameExistsAsync(user.Username))
                return Conflict("Username already exists.");

            if (await userDB.EmailExistsAsync(user.Email))
                return Conflict("Email already exists.");

            try
            {
                await userDB.AddUserAsync(user, roleName);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }

            return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
        }

        // התחברות למערכת (שם משתמש וסיסמה)
        [HttpPost("login")]
        public async Task<ActionResult<User>> Login([FromBody] User credentials)
        {
            User? user = await userDB.CheckLoginAsync(credentials.Username, credentials.Password);
            return user == null ? Unauthorized() : Ok(user);
        }

        // עדכון תפקיד משתמש
        [HttpPut("{id:int}/role")]
        public async Task<IActionResult> UpdateRole(int id, [FromQuery] string roleName)
        {
            return await userDB.UpdateUserRoleAsync(id, roleName) ? NoContent() : NotFound();
        }

        // עדכון סיסמה
        [HttpPut("{id:int}/password")]
        public async Task<IActionResult> UpdatePassword(int id, [FromBody] string newPassword)
        {
            return await userDB.UpdatePasswordAsync(id, newPassword) ? NoContent() : NotFound();
        }

        // עדכון אימייל
        [HttpPut("{id:int}/email")]
        public async Task<IActionResult> UpdateEmail(int id, [FromBody] string newEmail)
        {
            if (await userDB.EmailExistsAsync(newEmail))
                return Conflict("Email already exists.");

            return await userDB.UpdateEmailAsync(id, newEmail) ? NoContent() : NotFound();
        }

        // מחיקה רכה (כיבוי IsActive)
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            return await userDB.SetUserActiveStatusAsync(id, false) ? NoContent() : NotFound();
        }
    }
}