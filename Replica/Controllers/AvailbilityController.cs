
using Replica.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;

namespace Replica.Controllers
{
    public class AvailbilityController : ApiController
    {
        private readonly ReplicaAirbnbEntities1 _context;

        public AvailbilityController()
        {
            _context = new ReplicaAirbnbEntities1();
        }

        public AvailbilityController(ReplicaAirbnbEntities1 context)
        {
            _context = context;
        }
        public class NearbyPropertyDto
        {
            public int OwnerId { get; set; }
            public string Name { get; set; }
            public string Image { get; set; }
            public int PropertyId { get; set; }
            public string Title { get; set; }
            public decimal Price { get; set; }
            public int? NoFloors { get; set; }
            public int? NoOfBeds { get; set; }
            public int? Rooms { get; set; }
            public int? Bathroom { get; set; }
            public int? Kitchen { get; set; }
            public string Description { get; set; }
            public double DistanceFromFamousPlace { get; set; }
            public string PlaceType { get; set; }
            public string PlaceSubtype { get; set; }
            public List<string> Photos { get; set; }
            public List<string> Services { get; set; }
            public string BookingStatus { get; set; } // Add this property
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
        }

        [HttpGet]

        public async Task<HttpResponseMessage> ab(int userId)
        {
            try
            {
                string baseUrl = $"{Request.RequestUri.Scheme}://{Request.RequestUri.Host}";

                var userProperties = await _context.Places
                    .Where(p => p.user_id == userId)
                    .ToListAsync();

                var resultProperties = new List<NearbyPropertyDto>();
                foreach (var place in userProperties)
                {
                  
                    var isUnavailable = await _context.CheckAvailabilities
                        .AnyAsync(ca => ca.place_id == place.place_id && ca.status == 1);

                    if (isUnavailable)
                    {
                        continue; 
                    }

                    
                    var bookings = await _context.Bookings
                        .Where(b => b.place_id == place.place_id)
                        .ToListAsync();

                    
                    string bookingStatus = "No Bookings"; 

                    if (bookings.Any())
                    {
                        
                        var bookingWithStatus1 = bookings.FirstOrDefault(b => b.booking_status == 1);
                        if (bookingWithStatus1 != null)
                        {
                            continue; 
                        }

                        
                        var bookingWithStatus0 = bookings.FirstOrDefault(b => b.booking_status == 0);
                        var bookingWithStatusm1 = bookings.FirstOrDefault(b => b.booking_status == -1);
                        if (bookingWithStatus0 != null)
                        {
                            bookingStatus = "Pending Request";
                        }
                        
                    }

                   
                    var placeType = await _context.Placetypes.FirstOrDefaultAsync(pt => pt.placetype_id == place.placetype_id);
                    var placeSubtype = await _context.PlaceSubtypes.FirstOrDefaultAsync(ps => ps.subtype_id == place.subtype_id);
                    var userdetails = await _context.Users.FirstOrDefaultAsync(u => u.user_id == place.user_id);

                    var photoFileNames = await _context.Photogalleries
                        .Where(pg => pg.place_id == place.place_id)
                        .Select(pg => pg.image)
                        .ToListAsync();

                    var services = await _context.PlaceServices
                        .Where(ps => ps.place_id == place.place_id)
                        .Select(ps => ps.Service.service_name)
                        .ToListAsync();

                    string userImageUrl = userdetails != null
                        ? $"{baseUrl}/Replica/Content/Uploads/Images/{userdetails.image}"
                        : null;

                    var photos = photoFileNames
                        .Select(fileName => fileName.StartsWith("http")
                            ? fileName
                            : $"{baseUrl}/Replica/Content/Uploads/Property/{place.place_id}/{Path.GetFileName(fileName)}")
                        .Distinct()
                        .ToList();

                    resultProperties.Add(new NearbyPropertyDto
                    {
                        OwnerId = userdetails.user_id,
                        Name = userdetails.name,
                        Image = userImageUrl,
                        PropertyId = place.place_id,
                        Title = place.title,
                        Price = place.price,
                        NoFloors = place.nofloors,
                        NoOfBeds = place.no_of_beds,
                        Rooms = place.rooms,
                        Bathroom = place.bathroom,
                        Kitchen = place.kitchen,
                        Description = place.description,
                        PlaceType = placeType?.placetype_name,
                        PlaceSubtype = placeSubtype?.subtype_name,
                        Photos = photos,
                        Services = services,
                        BookingStatus = bookingStatus,
                    });
                }

                if (resultProperties.Any())
                    return Request.CreateResponse(HttpStatusCode.OK, resultProperties);
                else
                    return Request.CreateResponse(HttpStatusCode.NotFound, "No properties found for the specified user with the given filters.");
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, "An error occurred: " + ex.Message);
            }
        }


    }
}
