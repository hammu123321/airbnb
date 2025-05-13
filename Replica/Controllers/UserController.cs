using Newtonsoft.Json;
using Replica.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;

namespace Replica.Controllers
{
    public class UserController : ApiController
    {

        ReplicaAirbnbEntities1 db = new ReplicaAirbnbEntities1();

        private string SaveFile(HttpPostedFile photo, string username, int userId)
        {
            
            string safeUsername = new string(username.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
            string fileExtension = Path.GetExtension(photo.FileName);
            string fileName = $"{userId}{safeUsername}{fileExtension}";

            string filePath = HttpContext.Current.Server.MapPath($"~/Content/Uploads/Images/{fileName}");

            photo.SaveAs(filePath);

            return fileName;
        }

        [HttpPost]
        public async Task<HttpResponseMessage> RegisterUser()
        {
            try
            {
                var req = HttpContext.Current.Request;
                string userJson = req["user"];
                if (string.IsNullOrEmpty(userJson))
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, "No user data provided");
                }

                User userDetail = JsonConvert.DeserializeObject<User>(userJson);
                if (userDetail == null)
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, "Invalid user data");
                }

                var photo = req.Files["profile"];
                if (photo == null)
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, "Profile picture is missing");
                }


                db.Users.Add(userDetail);
                await db.SaveChangesAsync();

                string savedFileName = SaveFile(photo, userDetail.name, userDetail.user_id);

                userDetail.image = savedFileName;
                await db.SaveChangesAsync();
                return Request.CreateResponse(HttpStatusCode.Created, new
                {
                    userId = userDetail.user_id, 
                  
                });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, "An error occurred: " + ex.ToString());
            }

        }



        [HttpGet]
        public HttpResponseMessage Login(string email, string password)
        {
            try
            {
                db.Configuration.ProxyCreationEnabled = false;

                Console.WriteLine($"Attempting to log in with email: {email}");


                var user = db.Users.FirstOrDefault(u => u.email == email && u.password == password);

                if (user != null)
                {

                    Console.WriteLine("Login successful");
                    return Request.CreateResponse(HttpStatusCode.OK, user);
                }
                else
                {

                    Console.WriteLine("Login failed - invalid email or password");
                    return Request.CreateResponse(HttpStatusCode.Unauthorized, "Invalid email or password");
                }
            }
            catch (Exception ex)
            {

                Console.WriteLine($"An error occurred: {ex.Message}");
                return Request.CreateResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }




    }
}
