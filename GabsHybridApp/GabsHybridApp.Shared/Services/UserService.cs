using GabsHybridApp.Shared.Data;
using GabsHybridApp.Shared.Models;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace GabsHybridApp.Shared.Services
{
    public class UserService
    {
        private readonly IDbContextFactory<HybridAppDbContext> _factory;

        public UserService(IDbContextFactory<HybridAppDbContext> factory)
        {
            _factory = factory;
        }

        public UserAccount? Authenticate(string username, string password)
        {
            CreateAdmin(); // Comment out this line if you already have admin account

            if (username.Contains("@"))
                if (username.Split('@')[0].Equals(UserAccount.DEFAULT_ADMIN_LOGIN, StringComparison.OrdinalIgnoreCase))
                    username = UserAccount.DEFAULT_ADMIN_LOGIN;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return null;

            var user = GetSingleUser(username);
            if (user == null)
                return null;
            if (!user.IsActive)
                return null;

            bool valid = VerifyPasswordHash(password, user.PasswordSalt, user.PasswordHash);
            if (valid)
            {
                using var db = _factory.CreateDbContext();
                var dbUser = db.UserAccounts.FirstOrDefault(u => u.Id == user.Id);
                if (dbUser != null)
                {
                    dbUser.LastLogin = DateTime.Now;
                    db.SaveChanges();
                }
                user.PasswordHash = null;
                user.PasswordSalt = null;
                return user;
            }

            return null;
        }

        public bool IsAccountPendingActivation(string? username)
        {
            if (string.IsNullOrWhiteSpace(username)) return false;
            var user = GetSingleUser(username.Trim());
            return user != null && !user.IsActive;
        }

        public Guid? Create(string? username, string? password, string? roles = "", bool requiresActivation = false)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return null;

            username = username.Trim();
            if (!Regex.IsMatch(username, @"^[a-zA-Z0-9_.@]*$"))
                return null;

            var user = new UserAccount
            {
                Id = Guid.NewGuid(),
                Username = username.Trim().ToLower()
            };

            using var db = _factory.CreateDbContext();
            var userExists = db.UserAccounts.Any(x => x.Username!.ToLower() == user.Username.ToLower());
            if (userExists)
                return null;

            // Create PasswordHash
            using (var hmac = new System.Security.Cryptography.HMACSHA512())
            {
                user.PasswordSalt = hmac.Key;
                user.PasswordHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
            }

            user.Roles = Regex.Replace(roles!, @"\s+", "");
            user.CreatedOn = DateTime.Now;
            user.IsActive = !requiresActivation;
            user.ServerSalt = Guid.NewGuid().ToString("N");

            db.UserAccounts.Add(user);
            db.SaveChanges();

            return user.Id;
        }

        public bool ChangePassword(string? username, string password = "", string? newPassword = "", bool forceChange = false)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(newPassword))
                return false;

            if (forceChange == false && string.IsNullOrWhiteSpace(password))
                return false;

            var user = GetSingleUser(username);
            if (user == null)
                return false;

            var validPassword = forceChange || VerifyPasswordHash(password, user.PasswordSalt, user.PasswordHash);
            if (validPassword)
            {
                using var db = _factory.CreateDbContext();
                var dbUser = db.UserAccounts.FirstOrDefault(u => u.Id == user.Id);
                if (dbUser == null) return false;

                // Overwrite with new PasswordHash
                using (var hmac = new System.Security.Cryptography.HMACSHA512())
                {
                    dbUser.PasswordSalt = hmac.Key;
                    dbUser.PasswordHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(newPassword));
                }

                dbUser.ServerSalt = Guid.NewGuid().ToString("N");
                db.SaveChanges();
                return true;
            }
            else
                return false;
        }

        public List<UserAccount> GetAllUsers()
        {
            EnsureSeedUsers();
            using var db = _factory.CreateDbContext();
            return db.UserAccounts.AsNoTracking().OrderBy(u => u.Username).ToList();
        }

        public async Task<List<UserAccount>> GetAllUsersAsync()
        {
            EnsureSeedUsers();
            await using var db = await _factory.CreateDbContextAsync();
            return await db.UserAccounts.AsNoTracking().OrderBy(u => u.Username).ToListAsync();
        }

        public async Task<UserAccount?> GetUserByIdAsync(Guid id)
        {
            await using var db = await _factory.CreateDbContextAsync();
            return await db.UserAccounts.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<UserAccount?> GetUserByUsernameAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return null;
            await using var db = await _factory.CreateDbContextAsync();
            return await db.UserAccounts.AsNoTracking().FirstOrDefaultAsync(u => u.Username != null && u.Username.ToLower() == username.Trim().ToLower());
        }

        public bool DeleteUser(Guid userId)
        {
            using var db = _factory.CreateDbContext();
            var user = db.UserAccounts.FirstOrDefault(u => u.Id == userId);
            if (user == null) return false;
            if (user.Username!.Equals(UserAccount.DEFAULT_ADMIN_LOGIN, StringComparison.OrdinalIgnoreCase)) return false; // Prevent deleting default admin

            db.UserAccounts.Remove(user);
            db.SaveChanges();
            return true;
        }

        public bool DeleteUser(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return false;
            if (username.Equals(UserAccount.DEFAULT_ADMIN_LOGIN, StringComparison.OrdinalIgnoreCase)) return false;

            using var db = _factory.CreateDbContext();
            var user = db.UserAccounts.FirstOrDefault(u => u.Username!.ToLower() == username.Trim().ToLower());
            if (user == null) return false;

            db.UserAccounts.Remove(user);
            db.SaveChanges();
            return true;
        }

        public bool SetRoles(string username, string roles)
        {
            using var db = _factory.CreateDbContext();
            var user = db.UserAccounts.FirstOrDefault(u => u.Username!.ToLower() == username.Trim().ToLower());
            if (user != null)
            {
                var roleList = (roles ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // Prevent removing administrator role from default admin account
                if (user.Username!.Equals(UserAccount.DEFAULT_ADMIN_LOGIN, StringComparison.OrdinalIgnoreCase))
                {
                    if (!roleList.Any(r => r.Equals(UserAccount.DEFAULT_ADMIN_ROLENAME, StringComparison.OrdinalIgnoreCase) || r.Equals("admin", StringComparison.OrdinalIgnoreCase)))
                    {
                        roleList.Insert(0, UserAccount.DEFAULT_ADMIN_ROLENAME);
                    }
                }

                var cleanRoles = string.Join(",", roleList);

                user.Roles = cleanRoles;
                db.SaveChanges();
                return true;
            }
            return false;
        }

        public int BatchImportUsers(IEnumerable<(string username, string password, string roles, bool isActive)> users)
        {
            using var db = _factory.CreateDbContext();
            int importedCount = 0;
            foreach (var item in users)
            {
                if (string.IsNullOrWhiteSpace(item.username) || string.IsNullOrWhiteSpace(item.password))
                    continue;

                var existing = db.UserAccounts.Any(x => x.Username!.ToLower() == item.username.Trim().ToLower());
                if (existing)
                    continue;

                var id = Create(item.username, item.password, item.roles, requiresActivation: !item.isActive);
                if (id.HasValue)
                    importedCount++;
            }
            return importedCount;
        }

        private void CreateAdmin()
        {
            using var db = _factory.CreateDbContext();
            var hasAdmin = db.UserAccounts.Any(x => x.Roles == UserAccount.DEFAULT_ADMIN_ROLENAME || x.Username == UserAccount.DEFAULT_ADMIN_LOGIN);
            if (!hasAdmin)
            {
                Create(UserAccount.DEFAULT_ADMIN_LOGIN, UserAccount.DEFAULT_ADMIN_LOGIN, UserAccount.DEFAULT_ADMIN_ROLENAME);
            }
        }

        private void EnsureSeedUsers()
        {
            CreateAdmin();
        }

        public UserAccount? GetSingleUser(string username)
        {
            using var db = _factory.CreateDbContext();
            return db.UserAccounts.AsNoTracking().SingleOrDefault(x => x.Username!.ToLower() == username.Trim().ToLower());
        }

        private bool VerifyPasswordHash(string userPassword, byte[]? passwordSalt, byte[]? passwordHash)
        {
            if (passwordSalt == null || passwordHash == null) return false;
            // Verify PasswordHash
            using (var hmac = new System.Security.Cryptography.HMACSHA512(passwordSalt))
            {
                var computedHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(userPassword));
                for (int i = 0; i < computedHash.Length; i++)
                {
                    if (computedHash[i] != passwordHash[i])
                        return false;
                }
            }

            return true;
        }

        public string SetActivation(string username, bool isActive)
        {
            using var db = _factory.CreateDbContext();
            var user = db.UserAccounts.FirstOrDefault(u => u.Username!.ToLower() == username.Trim().ToLower());
            if (user != null)
            {
                if (user.Username!.Equals(UserAccount.DEFAULT_ADMIN_LOGIN, StringComparison.OrdinalIgnoreCase))
                {
                    user.IsActive = true;
                    return "default admin cannot be deactivated";
                }

                user.IsActive = isActive;
                if (!isActive)
                {
                    user.ServerSalt = Guid.NewGuid().ToString("N");
                }
                db.SaveChanges();
                return "user is " + (user.IsActive ? "active" : "inactive");
            }

            return "user not found";
        }

        public bool AssignRoles(string username, string roles = "")
        {
            using var db = _factory.CreateDbContext();
            var user = db.UserAccounts.FirstOrDefault(u => u.Username!.ToLower() == username.Trim().ToLower());
            if (user != null)
            {
                roles = Regex.Replace(roles!, @"\s+", "");
                var arrRoles = (user.Roles ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Concat(roles.Split(',')).Distinct();
                user.Roles = string.Join(",", arrRoles);
                db.SaveChanges();
                return true;
            }

            return false;
        }

        public bool RemoveRoles(string username, string roles = "")
        {
            using var db = _factory.CreateDbContext();
            var user = db.UserAccounts.FirstOrDefault(u => u.Username!.ToLower() == username.Trim().ToLower());
            if (user != null)
            {
                roles = Regex.Replace(roles!, @"\s+", "");
                var rolesToRemove = roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var arrRoles = (user.Roles ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(x => !rolesToRemove.Contains(x, StringComparer.OrdinalIgnoreCase))
                    .ToList();

                if (user.Username!.Equals(UserAccount.DEFAULT_ADMIN_LOGIN, StringComparison.OrdinalIgnoreCase))
                {
                    if (!arrRoles.Any(r => r.Equals(UserAccount.DEFAULT_ADMIN_ROLENAME, StringComparison.OrdinalIgnoreCase) || r.Equals("admin", StringComparison.OrdinalIgnoreCase)))
                    {
                        arrRoles.Insert(0, UserAccount.DEFAULT_ADMIN_ROLENAME);
                    }
                }

                user.Roles = string.Join(",", arrRoles);
                db.SaveChanges();
                return true;
            }

            return false;
        }

        public List<string> GetLocalUsernames()
        {
            using var db = _factory.CreateDbContext();
            return db.UserAccounts
                .AsNoTracking()
                .Where(u => !string.IsNullOrWhiteSpace(u.Username))
                .Select(u => u.Username!)
                .ToList();
        }

        public int UpsertSyncedUsers(IEnumerable<UserAccount> users)
        {
            if (users == null) return 0;
            using var db = _factory.CreateDbContext();
            int count = 0;

            foreach (var synced in users)
            {
                if (string.IsNullOrWhiteSpace(synced.Username)) continue;

                var existing = db.UserAccounts.FirstOrDefault(u =>
                    u.Id == synced.Id ||
                    (u.Username != null && u.Username.ToLower() == synced.Username.ToLower()));

                if (existing != null)
                {
                    // Update credentials and role states
                    existing.Username = synced.Username;
                    existing.Roles = synced.Roles;
                    existing.IsActive = synced.IsActive;
                    existing.ServerSalt = synced.ServerSalt;
                    if (synced.PasswordHash != null && synced.PasswordSalt != null)
                    {
                        existing.PasswordHash = synced.PasswordHash;
                        existing.PasswordSalt = synced.PasswordSalt;
                    }
                }
                else
                {
                    // Insert new cached user
                    var newUser = new UserAccount
                    {
                        Id = synced.Id == Guid.Empty ? Guid.NewGuid() : synced.Id,
                        Username = synced.Username.Trim().ToLower(),
                        PasswordHash = synced.PasswordHash,
                        PasswordSalt = synced.PasswordSalt,
                        Roles = synced.Roles,
                        IsActive = synced.IsActive,
                        ServerSalt = synced.ServerSalt,
                        CreatedOn = synced.CreatedOn == default ? DateTime.Now : synced.CreatedOn,
                        LastLogin = synced.LastLogin
                    };
                    db.UserAccounts.Add(newUser);
                }
                count++;
            }

            if (count > 0)
            {
                db.SaveChanges();
            }

            return count;
        }
    }
}

namespace GabsHybridApp.Shared.Models
{
    public class UserAccount
    {
        // Change this to your desired default admin login, password and role name.
        public const string DEFAULT_ADMIN_LOGIN = "admin";
        public const string DEFAULT_ADMIN_ROLENAME = "administrator";

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        [Required]
        [StringLength(50)]
        public string? Username { get; set; }
        [JsonIgnore]
        public byte[]? PasswordHash { get; set; }
        [JsonIgnore]
        public byte[]? PasswordSalt { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? LastLogin { get; set; }
        public bool IsActive { get; set; }
        public string? Roles { get; set; } // comma-separated

        // NEW: per-user revocation / rotation for HMAC derivation
        public string? ServerSalt { get; set; }
    }
}
