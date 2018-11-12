using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using System.Web.Security;
using Sprinter.Extensions;

namespace Sprinter.Models
{
    public class RoleInfo
    {
        public Guid RoleID { get; set; }

        public string RoleName { get; set; }

        public bool Selected { get; set; }
    }

    public partial class User
    {
        public bool CanDelete
        {
            get { return UserName.ToLower() != "admin"; }
        }

        public UserProfile Profile
        {
            get
            {
                if (UserProfile == null)
                    return new UserProfile() { User = this };
                return UserProfile;
            }

        }
    }

    [MetadataType(typeof(ProfileDataAnnotations))]
    public partial class UserProfile
    {
        public IEnumerable<RoleInfo> RolesList
        {
            get
            {
                var db = new DB();
                var allRoles = db.Roles;
                return
                    allRoles.AsEnumerable().Select(
                        x =>
                        new RoleInfo()
                            {
                                RoleName = x.RoleName,
                                RoleID = x.RoleId,
                                Selected = User.UsersInRoles.Select(z => z.RoleId).Any(c => c == x.RoleId)
                            });
            }
        }

        public string NewPassword { get; set; }


        private string _login;
        public string Login
        {
            get
            {
                if (_login.IsNullOrEmpty())
                {
                    if (MembershipUser != null)
                        _login = MembershipUser.UserName;
                }
                return _login;
            }
            set { _login = value; }
        }

        public string SurnameAndName
        {
            get
            {
                return "{0} {1}".FormatWith(new string[] { Surname, Name });
            }
        }
        public string FullName
        {
            get
            {
                var name = "{0} {1} {2}".FormatWith(new string[] { Surname, Name, Patrinomic });
                if (name.IsNullOrEmpty()) return "[Anonimous]";
                return name;
            }
        }

        private MembershipUser user = null;
        public MembershipUser MembershipUser
        {
            get
            {
                if (user == null)
                {

                    user = Membership.GetUser(UserID);
                }
                return user;
            }
            set { user = value; }
        }
        private string _mail;
        public string Email
        {
            get
            {
                if (!_mail.IsNullOrEmpty())
                {
                    return _mail;
                }
                if (MembershipUser != null)
                {
                    return MembershipUser.Email;
                }
                return "";
            }
            set
            {
                _mail = value;
            }
        }
        public string Password
        {
            get
            {
                if (MembershipUser != null)
                {
                    return MembershipUser.GetPassword();
                }
                return NewPassword;
            }
            set
            {
                NewPassword = value;
            }
        }

        public string FullAdress
        {
            get
            {
                var filled = new List<string>();
                if (ZipCode.IsFilled()) filled.Add(ZipCode);
                if (Region.IsFilled()) filled.Add(Region);
                if (Town.IsFilled()) filled.Add(Town);
                if (Street.IsFilled()) filled.Add(Street);
                if (House.IsFilled()) filled.Add(House);
                if (Building.IsFilled()) filled.Add(Building);
                if (Flat.IsFilled()) filled.Add(Flat);
                if (Doorway.IsFilled()) filled.Add("подъезд " + Doorway);
                if (Floor.IsFilled()) filled.Add("этаж " + Floor);
                return string.Join(", ", filled.ToArray());
            }
        }

        public string FullAdressForPayment
        {
            get
            {
                var filled = new List<string>();
                if (ZipCode.IsFilled()) filled.Add(ZipCode);
                if (Region.IsFilled()) filled.Add(Region);
                if (Town.IsFilled()) filled.Add(Town);
                if (Street.IsFilled()) filled.Add(Street);
                if (House.IsFilled()) filled.Add(House);
                if (Building.IsFilled() && Building!="-") filled.Add(Building);
                if (Flat.IsFilled()) filled.Add(Flat);
                return string.Join(", ", filled.ToArray());
            }
        }


        public class ProfileDataAnnotations
        {

            [Required(AllowEmptyStrings = false, ErrorMessage = "Поле '{0}' обязательно для заполнения")]
            [DisplayName("Пароль *"), StringLength(100, ErrorMessage = "{0} должен содержать минимум {2} символов.", MinimumLength = 6)]
            public string Password { get; set; }


            [DisplayName("Логин *")]
            [Required(AllowEmptyStrings = false, ErrorMessage = "Поле '{0}' обязательно для заполнения")]
            public string Login { get; set; }

            [DisplayName("Email*")]
            [Required(AllowEmptyStrings = false, ErrorMessage = "Поле '{0}' обязательно для заполнения")]
            [RegularExpression(@"^([a-zA-Z0-9_\-\.]+)@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.)|(([a-zA-Z0-9\-]+\.)+))([a-zA-Z]{2,4}|[0-9]{1,3})(\]?)$", ErrorMessage = "Пожалуйста укажите правильный Email адрес")]
            public string Email { get; set; }

            [Required(AllowEmptyStrings = false, ErrorMessage = "Поле '{0}' обязательно для заполнения")]
            [DisplayName("Имя")]
            public string Name { get; set; }

            [DisplayName("Фамилия")]
            public string Surname { get; set; }

            [DisplayName("Отчество")]
            public string Patrinomic { get; set; }

            [DisplayName("Телефон")]
            public string HomePhone { get; set; }

            [DisplayName("Мобильный телефон")]
            public string MobilePhone { get; set; }

            [DisplayName("Регион")]
            public string Region { get; set; }

            [DisplayName("Город")]
            public string Town { get; set; }

            [DisplayName("Адрес")]
            public string Address { get; set; }

            [DisplayName("Улица")]
            public string Street { get; set; }

            [DisplayName("Номер дома")]
            public string House { get; set; }

            [DisplayName("Корпус")]
            public string Building { get; set; }

            [DisplayName("Подъезд")]
            public string Doorway { get; set; }

            [DisplayName("Квартира")]
            public string Flat { get; set; }

            [DisplayName("Индекс")]
            public string ZipCode { get; set; }

            [DisplayName("Этаж")]
            public string Floor { get; set; }


            [DisplayName("Метро")]
            public string Metro { get; set; }



            [DisplayName("Название организации")]
            public string OrgName { get; set; }

            [DisplayName("ИНН")]
            public string OrgINN { get; set; }

            [DisplayName("КПП")]
            public string OrgKPP { get; set; }

            [DisplayName("К/с")]
            public string OrgKS { get; set; }

            [DisplayName("Р/с")]
            public string OrgRS { get; set; }

            [DisplayName("Банк")]
            public string OrgBank { get; set; }

            [DisplayName("БИК")]
            public string OrgBik { get; set; }

            [DisplayName("Юр. адрес")]
            public string OrgJurAddr { get; set; }

            [DisplayName("Факт. адрес")]
            public string OrgFactAddr { get; set; }

            [DisplayName("Генеральный дир.")]
            public string OrgDirector { get; set; }

            [DisplayName("Главный бух.")]
            public string OrgAccountant { get; set; }


        }

    }
}