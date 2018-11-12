using System;
using System.Collections.Generic;
using System.Data.Linq.SqlClient;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Security;
using Sprinter.Extensions;
using Sprinter.Models;

namespace Sprinter.Controllers
{
    public class UsersController : Controller
    {
        private DB db = new DB();

        [AuthorizeMaster]
        [HttpGet]
        public ActionResult Index(Guid? role, string query, int? page)
        {
            var roles = db.Roles.Select(x => new { Key = x.RoleId, Value = x.RoleName }).ToList();
            roles.Insert(0, new { Key = new Guid(), Value = "Все роли" });

            ViewBag.Roles = new SelectList(roles, "Key", "Value");

            var users = db.Users.AsQueryable();
            if (role.HasValue && !role.IsEmpty())
                users = users.Where(x => x.UsersInRoles.Any(z => z.RoleId == role));



            if (!query.IsNullOrEmpty())
            {
                query = "%{0}%".FormatWith(query);
                users =
                    users.Where(
                        x =>
                        SqlMethods.Like(x.UserName.ToLower(), query.ToLower()) ||
                        SqlMethods.Like(x.MembershipData.Email.ToLower(), query.ToLower()) ||
                        SqlMethods.Like(x.UserProfile.Name.ToLower(), query.ToLower()) ||
                        SqlMethods.Like(x.UserProfile.Patrinomic.ToLower(), query.ToLower()) ||
                        SqlMethods.Like(x.UserProfile.Surname.ToLower(), query.ToLower()));
            }

            if (((page ?? 0 + 1) * 50) > users.Count())
                page = 0;

            return
                View(new PagedData<User>(users.OrderBy(x => x.UserName), page ?? 0, 50, "Master",
                                         new RouteValueDictionary(
                                             new { query = (query ?? "").Replace("%", ""), page = page, role = role })));
        }

        [AuthorizeMaster]
        [HttpPost]
        public ActionResult Index(Guid? role, string query, int? page, FormCollection collection)
        {
            return RedirectToAction("Index", new { query = query, page = page, role = role });
        }

        [AuthorizeMaster]
        [HttpGet]
        public ActionResult Delete(Guid? user, Guid? role, string query, int? page)
        {
            var u = db.Users.FirstOrDefault(x => x.UserId == user);
            if (u == null) return RedirectToAction("Index", new { query = query, page = page, role = role });
            return View(u);
        }

        [AuthorizeMaster]
        [HttpPost]
        public ActionResult Delete(Guid? user, Guid? role, string query, int? page, FormCollection collection)
        {
            var u = db.Users.FirstOrDefault(x => x.UserId == user);
            if (u == null) return RedirectToAction("Index", new { query = query, page = page, role = role });
            if (u.CanDelete)
                Membership.DeleteUser(u.Profile.MembershipUser.UserName);
            else
            {
                ModelState.AddModelError("", "Удаление данного пользователя запрещено.");
                return View(u);
            }
            return RedirectToAction("Index", new { query = query, page = page, role = role });
        }

        [AuthorizeMaster]
        [HttpGet]
        public ActionResult Edit(Guid? role, string query, int? page, Guid? user)
        {
            var u = db.Users.FirstOrDefault(x => x.UserId == user) ?? new User();
            return View(u.Profile);
        }

        [AuthorizeMaster]
        [HttpPost]
        public ActionResult Edit(Guid? role, string query, int? page, Guid? user, FormCollection collection)
        {
            MembershipUser us;
            if (user.IsEmpty())
            {
                try
                {
                    var exist = Membership.GetUser(collection["Login"]);
                    if (exist != null)
                    {
                        ModelState.AddModelError("", "Пользователь с таким логином уже существует.");
                        var newProfile = new UserProfile();
                        TryUpdateModel(newProfile,
                                       new[]
                                           {
                                               "Name", "Surname", "Patrinomic", "HomePhone", "MobilePhone", "Region",
                                               "ZipCode", "Town", "Street", "House", "Building", "Doorway", "Flat","Floor",
                                               "Metro", "Password", "Email", "Login","Address"
                                           });
                        return View(newProfile);
                    }
                    if (Membership.FindUsersByEmail(collection["Email"]).Count > 0)
                    {
                        ModelState.AddModelError("", "Пользователь с таким Email уже существует.");
                        var newProfile = new UserProfile();
                        TryUpdateModel(newProfile,
                                       new[]
                                           {
                                               "Name", "Surname", "Patrinomic", "HomePhone", "MobilePhone", "Region",
                                               "ZipCode", "Town", "Street", "House", "Building", "Doorway", "Flat","Floor",
                                               "Metro", "Password", "Email", "Login","Address"
                                           });
                        return View(newProfile);
                    }
                    us = Membership.CreateUser(collection["Login"], collection["Password"], collection["Email"]);
                }
                catch (Exception e)
                {
                    ModelState.AddModelError("", e.Message);
                    var newProfile = new UserProfile();
                    UpdateModel(newProfile,
                                new[]
                                    {
                                        "Name", "Surname", "Patrinomic", "HomePhone", "MobilePhone", "Region",
                                        "ZipCode", "Town", "Street", "House", "Building", "Doorway", "Flat","Floor",
                                        "Metro", "Password", "Email", "Login","Address"
                                    });
                    return View(newProfile);

                }
            }
            else
            {
                us = Membership.GetUser(user);
            }

            var profile = db.UserProfiles.FirstOrDefault(x => x.UserID == (Guid)us.ProviderUserKey);
            if (profile == null)
            {
                profile = new UserProfile() { UserID = (Guid)us.ProviderUserKey };
                profile.MembershipUser = us;
                db.UserProfiles.InsertOnSubmit(profile);
            }

            UpdateModel(profile,
                        new[]
                            {
                                "Name", "Surname", "Patrinomic", "HomePhone", "MobilePhone", "Region", "ZipCode", "Town"
                                , "Street", "House", "Building", "Doorway", "Flat","Floor",
                                "Metro", "Password", "Email","Address"
                            });

            if (profile.NewPassword != profile.Password && profile.MembershipUser != null)
            {
                us.ChangePassword(profile.Password, profile.NewPassword);
            }

            if (profile.MembershipUser != null && profile.MembershipUser.Email != profile.Email)
            {
                us.Email = profile.Email;
                Membership.UpdateUser(us);
            }

            db.SubmitChanges();

            if (profile.User.CanDelete)
            {
                SaveRoles(profile.UserID, collection);
            }


            return RedirectToAction("Index", new { query = query, page = page, role = role });
        }

        private void SaveRoles(Guid userId, FormCollection collection)
        {
            UsersInRole role;
            var roles = from x in db.Roles select x;
            var exist = (from x in db.UsersInRoles
                         where x.UserId == userId
                         select x.RoleId).AsEnumerable<Guid>();
            var provider = collection.ToValueProvider();
            var selected = from x in roles.AsEnumerable()
                           where (bool)provider.GetValue(x.RoleName).ConvertTo(typeof(bool))
                           select x.RoleId;
            var forAdd = selected.Except(exist);
            var forDel = exist.Except(selected);
            using (IEnumerator<Guid> enumerator = forDel.GetEnumerator())
            {
                Guid guid;
                while (enumerator.MoveNext())
                {
                    guid = enumerator.Current;
                    role = (from x in db.UsersInRoles
                            where (x.UserId == userId) && (x.RoleId == guid)
                            select x).First();
                    db.UsersInRoles.DeleteOnSubmit(role);
                }
            }
            foreach (Guid guid in forAdd)
            {
                var usersInRole = new UsersInRole
                {
                    RoleId = guid,
                    UserId = userId
                };

                db.UsersInRoles.InsertOnSubmit(usersInRole);
            }
            db.SubmitChanges();
        }

    }
}
