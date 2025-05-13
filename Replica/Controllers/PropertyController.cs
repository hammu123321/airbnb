using Newtonsoft.Json;
using Replica.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Core.Metadata.Edm;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.AccessControl;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Hosting;
using System.Web.Http;

namespace Replica.Controllers
{
    public class PropertyController : ApiController
    {
        private readonly ReplicaAirbnbEntities1 _context;

        public PropertyController()
        {
            _context = new ReplicaAirbnbEntities1();
        }

        public PropertyController(ReplicaAirbnbEntities1 context)
        {
            _context = context;
        }

        public class PlaceDataRequest
        {
            public Placetype PlaceType { get; set; }
            public PlaceSubtype PlaceSubtype { get; set; }
            public Place Place { get; set; }

            public List<Service> Services { get; set; }
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
            public decimal AverageRating { get; set; }

            public List<string> BookingStartDates { get; set; }  // List to store multiple booking start dates
            public List<string> BookingEndDates { get; set; }    // List to s
        }


        [HttpPost]
        public async Task<HttpResponseMessage> SubmitPlaceData()
        {
            var req = HttpContext.Current.Request;

            var placeDataJson = req.Form["data"];
            if (string.IsNullOrEmpty(placeDataJson))
                return Request.CreateResponse(HttpStatusCode.BadRequest, "No place data provided.");

            var placeData = Newtonsoft.Json.JsonConvert.DeserializeObject<PlaceDataRequest>(placeDataJson);

            if (placeData == null || placeData.Place == null || placeData.PlaceType == null || placeData.PlaceSubtype == null)
                return Request.CreateResponse(HttpStatusCode.BadRequest, "Incomplete place data provided.");

            try
            {
                var placeType = await _context.Placetypes
                                               .SingleOrDefaultAsync(pt => pt.placetype_name == placeData.PlaceType.placetype_name)
                                   ?? new Placetype { placetype_name = placeData.PlaceType.placetype_name };

                if (placeType.placetype_id == 0)
                {
                    _context.Placetypes.Add(placeType);
                    await _context.SaveChangesAsync();
                }

                
                var placeSubtype = await _context.PlaceSubtypes
                                                 .SingleOrDefaultAsync(st => st.subtype_name == placeData.PlaceSubtype.subtype_name)
                                      ?? new PlaceSubtype { subtype_name = placeData.PlaceSubtype.subtype_name };

                if (placeSubtype.subtype_id == 0)
                {
                    _context.PlaceSubtypes.Add(placeSubtype);
                    await _context.SaveChangesAsync();
                }

               
                var newPlace = new Place
                {
                    user_id = placeData.Place.user_id,
                    placetype_id = placeType.placetype_id,
                    subtype_id = placeSubtype.subtype_id,
                    title = placeData.Place.title,
                    price = placeData.Place.price,
                    nofloors = placeData.Place.nofloors,
                    no_of_beds = placeData.Place.no_of_beds,
                    description = placeData.Place.description,
                    latitude = placeData.Place.latitude,
                    longitude = placeData.Place.longitude,
                    rooms = placeData.Place.rooms,
                    bathroom = placeData.Place.bathroom,
                    kitchen = placeData.Place.kitchen
                };

                _context.Places.Add(newPlace);
                await _context.SaveChangesAsync();

                
                if (req.Files.Count > 0)
                {
                    for (int i = 0; i < req.Files.Count; i++)
                    {
                        var photo = req.Files[i];
                        if (photo != null)
                        {
                            FileInfo fileInfo = new FileInfo(photo.FileName);
                            string ext = fileInfo.Extension;
                            string key = $"image{i}";

                            string fileName = $"{key}_{newPlace.place_id}{ext}";
                            string directoryPath = HttpContext.Current.Server.MapPath($"~/Content/Uploads/Property/{newPlace.place_id}");

                            if (!Directory.Exists(directoryPath))
                            {
                                Directory.CreateDirectory(directoryPath);
                            }

                            string filePath = Path.Combine(directoryPath, fileName);
                            photo.SaveAs(filePath);

                            Photogallery pim = new Photogallery
                            {
                                place_id = newPlace.place_id,
                                image = $"Content/Upload/property/{newPlace.place_id}/{fileName}"
                            };
                            _context.Photogalleries.Add(pim);
                        }
                    }

                    await _context.SaveChangesAsync();
                }

              
                if (placeData.Services != null)
                {
                    foreach (var service in placeData.Services)
                    {
                        var existingService = _context.Services.FirstOrDefault(s => s.service_name == service.service_name);
                        if (existingService == null)
                        {
                            _context.Services.Add(service);
                            await _context.SaveChangesAsync();
                            existingService = service;
                        }

                        var placeService = new PlaceService
                        {
                            place_id = newPlace.place_id,
                            service_id = existingService.service_id
                        };
                        _context.PlaceServices.Add(placeService);
                    }

                    await _context.SaveChangesAsync();
                }


                return Request.CreateResponse(HttpStatusCode.Created, new { placeId = newPlace.place_id, message = "Place added successfully." });
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, "An error occurred while processing your request.");
            }


        }
        [HttpGet]
        public async Task<HttpResponseMessage> GetPropertiesNearFamousPlace(string fm, decimal minPrice, decimal maxPrice, int noofrooms)
        {
            try
            {
                var famousPlace = _context.Nearbies.FirstOrDefault(fp => fp.typename == fm);

                if (famousPlace == null)
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "Famous place not found.");
                }

                double famousLatitude = Convert.ToDouble(famousPlace.latitude);
                double famousLongitude = Convert.ToDouble(famousPlace.longitude);

                string baseUrl = $"{Request.RequestUri.Scheme}://{Request.RequestUri.Host}";

                // Filtering places based on booking and availability status
                var validPlaceIds = _context.Bookings
                    .Where(b => b.booking_status != 1)
                    .Select(b => b.place_id)
                    .Concat(_context.CheckAvailabilities
                        .Where(a => a.status != 1)
                        .Select(a => a.place_id))
                    .Distinct();

                var properties = await _context.Places
                    .Where(p => p.price >= minPrice && p.price <= maxPrice && p.rooms >= noofrooms && validPlaceIds.Contains(p.place_id))
                    .Select(p => new
                    {
                        Property = p,
                        AverageRating = _context.ReviewsAndRatings
                                          .Where(r => r.property_reviewee_id == p.place_id)
                                          .Average(r => r.property_rating) ?? 0
                    })
                    .OrderByDescending(p => p.AverageRating)
                    .ToListAsync();

                var nearbyProperties = new List<NearbyPropertyDto>();

                foreach (var prop in properties)
                {
                    double propertyLatitude = Convert.ToDouble(prop.Property.latitude);
                    double propertyLongitude = Convert.ToDouble(prop.Property.longitude);

                    double distance = CalculateDistance(
                        famousLatitude,
                        famousLongitude,
                        propertyLatitude,
                        propertyLongitude
                    );

                    if (distance <= 10.0)
                    {
                        var placeType = await _context.Placetypes.FirstOrDefaultAsync(pt => pt.placetype_id == prop.Property.placetype_id);
                        var placeSubtype = await _context.PlaceSubtypes.FirstOrDefaultAsync(ps => ps.subtype_id == prop.Property.subtype_id);
                        var userdetails = await _context.Users.FirstOrDefaultAsync(u => u.user_id == prop.Property.user_id);

                        var photoFileNames = await _context.Photogalleries
                            .Where(pg => pg.place_id == prop.Property.place_id)
                            .Select(pg => pg.image)
                            .ToListAsync();

                        var services = await _context.PlaceServices
                            .Where(ps => ps.place_id == prop.Property.place_id)
                            .Join(_context.Services,
                                  ps => ps.service_id,
                                  s => s.service_id,
                                  (ps, s) => s.service_name)
                            .ToListAsync();

                        string userImageUrl = userdetails != null
                            ? $"{baseUrl}/Replica/Content/Uploads/Images/{userdetails.image}"
                            : null;
                        var photos = photoFileNames
                            .Select(fileName => fileName.StartsWith("http")
                                ? fileName
                                : $"{baseUrl}/Replica/Content/Uploads/Property/{prop.Property.place_id}/{Path.GetFileName(fileName)}").Distinct()
                            .ToList();

                        nearbyProperties.Add(new NearbyPropertyDto
                        {
                            OwnerId = userdetails.user_id,
                            Name = userdetails?.name,
                            Image = userImageUrl,
                            PropertyId = prop.Property.place_id,
                            Title = prop.Property.title,
                            Price = prop.Property.price,
                            NoFloors = prop.Property.nofloors,
                            NoOfBeds = prop.Property.no_of_beds,
                            Rooms = prop.Property.rooms,
                            Bathroom = prop.Property.bathroom,
                            Kitchen = prop.Property.kitchen,
                            Description = prop.Property.description,
                            DistanceFromFamousPlace = distance,
                            PlaceType = placeType?.placetype_name,
                            PlaceSubtype = placeSubtype?.subtype_name,
                            Photos = photos,
                            Services = services,
                            AverageRating = prop.AverageRating
                        });
                    }
                }

                if (nearbyProperties.Any())
                    return Request.CreateResponse(HttpStatusCode.OK, nearbyProperties);
                else
                    return Request.CreateResponse(HttpStatusCode.NotFound, "No properties found within the specified criteria.");
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, "An error occurred: " + ex.Message);
            }
        }


        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double EarthRadiusKm = 6371.0;

            double dLat = DegreesToRadians(lat2 - lat1);
            double dLon = DegreesToRadians(lon2 - lon1);

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return EarthRadiusKm * c;
        }


        private double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }

        [HttpPut]
        public IHttpActionResult GetAllProperties()
        {
            var properties = _context.Places
                .Select(p => new
                {
                    property_id = p.place_id,

                    price = p.price,
                    latitude = p.latitude,
                    longitude = p.longitude,

                }).ToList();

            return Ok(properties);

        }



        [HttpPut]
        public async Task<HttpResponseMessage> GetPropertiesByUserId(int userId)
        {
            try
            {
                // Validate if the user exists
                var userExists = await _context.Users.AnyAsync(u => u.user_id == userId);
                if (!userExists)
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "User not found.");
                }

                var userProperties = await _context.Places
                    .Where(p => p.user_id == userId)
                    .ToListAsync();

                if (!userProperties.Any())
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "No properties found for this user.");
                }

                string baseUrl = $"{Request.RequestUri.Scheme}://{Request.RequestUri.Host}";

                var propertyDetails = new List<NearbyPropertyDto>();

                foreach (var property in userProperties)
                {
                    var placeType = await _context.Placetypes.FirstOrDefaultAsync(pt => pt.placetype_id == property.placetype_id);
                    var placeSubtype = await _context.PlaceSubtypes.FirstOrDefaultAsync(ps => ps.subtype_id == property.subtype_id);
                    var userDetails = await _context.Users.FirstOrDefaultAsync(u => u.user_id == property.user_id);

                    
                    var bookings = await _context.Bookings
                        .Where(b => b.place_id == property.place_id)
                        .ToListAsync();

                    var availability = await _context.CheckAvailabilities
                        .Where(ca => ca.place_id == property.place_id && ca.status == 1)
                        .ToListAsync();

                    string bookingStatus = availability.Any() ? "Not Available" : "Available";
                    List<string> bookingStartDates = new List<string>();
                    List<string> bookingEndDates = new List<string>();
                    if (bookings.Any(b => b.booking_status == 1))
                    {
                        bookingStatus = "Booked";
                        foreach (var booking in bookings.Where(b => b.booking_status == 1))
                        {
                            bookingStartDates.Add(booking.start_date.ToString("yyyy-MM-dd"));
                            bookingEndDates.Add(booking.end_date.ToString("yyyy-MM-dd"));
                        }
                    }
                    else if (bookings.Any(b => b.booking_status == 0))
                    {
                        bookingStatus = "Pending Booking";
                    }

                    var photoFileNames = await _context.Photogalleries
                        .Where(pg => pg.place_id == property.place_id)
                        .Select(pg => pg.image)
                        .ToListAsync();

                    var photos = photoFileNames
                        .Select(fileName => fileName.StartsWith("http")
                            ? fileName
                            : $"{baseUrl}/Replica/Content/Uploads/Property/{property.place_id}/{Path.GetFileName(fileName)}")
                        .ToList();
                    string userImageUrl = userDetails != null
                        ? $"{baseUrl}/Replica/Content/Uploads/Images/{userDetails.image}"
                        : null;

                    
                    var services = await _context.PlaceServices
                        .Where(ps => ps.place_id == property.place_id)
                        .Join(
                            _context.Services,
                            ps => ps.service_id,
                            s => s.service_id,
                            (ps, s) => s.service_name
                        )
                        .ToListAsync();

                    propertyDetails.Add(new NearbyPropertyDto
                    {
                        OwnerId = userDetails?.user_id ?? 0,
                        Name = userDetails?.name ?? "Unknown",
                        Image = userImageUrl,
                        PropertyId = property.place_id,
                        Title = property.title,
                        Price = property.price,
                        NoFloors = property.nofloors,
                        NoOfBeds = property.no_of_beds,
                        Rooms = property.rooms,
                        Bathroom = property.bathroom,
                        Kitchen = property.kitchen,
                        Description = property.description,
                        PlaceType = placeType?.placetype_name ?? "N/A",
                        PlaceSubtype = placeSubtype?.subtype_name ?? "N/A",
                        Photos = photos,
                        BookingStatus = bookingStatus,
                        BookingStartDates = bookingStartDates, 
                        BookingEndDates = bookingEndDates,    
                        Services = services
                    });
                }
                return Request.CreateResponse(HttpStatusCode.OK, propertyDetails);
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, $"An error occurred: {ex.Message}");
            }
        }




        [HttpGet]
        public async Task<HttpResponseMessage> GetUnavailablePropertiesByUserId(int userId)
        {
            try
            {

                var userExists = await _context.Users.AnyAsync(u => u.user_id == userId);
                if (!userExists)
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "User not found.");
                }


                var userProperties = await _context.Places
                    .Where(p => p.user_id == userId)
                    .Join(_context.CheckAvailabilities,
                          p => p.place_id,
                          ca => ca.place_id,
                          (p, ca) => new { Property = p, Availability = ca })
                    .Where(x => x.Availability.status == 1 && x.Availability.end_date > DateTime.Now)
                    .Select(x => new { x.Property, x.Availability.start_date, x.Availability.end_date })
                    .ToListAsync();

                if (!userProperties.Any())
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "No unavailable properties found for this user.");
                }

                string baseUrl = $"{Request.RequestUri.Scheme}://{Request.RequestUri.Host}";

                var propertyDetails = new List<NearbyPropertyDto>();

                foreach (var item in userProperties)
                {
                    var property = item.Property;
                    var startDate = item.start_date;
                    var endDate = item.end_date;


                    var placeType = await _context.Placetypes.FirstOrDefaultAsync(pt => pt.placetype_id == property.placetype_id);
                    var placeSubtype = await _context.PlaceSubtypes.FirstOrDefaultAsync(ps => ps.subtype_id == property.subtype_id);

                    var userDetails = await _context.Users.FirstOrDefaultAsync(u => u.user_id == property.user_id);

                    var photoFileNames = await _context.Photogalleries
                        .Where(pg => pg.place_id == property.place_id)
                        .Select(pg => pg.image)
                        .ToListAsync();


                    var photos = photoFileNames
                        .Select(fileName => fileName.StartsWith("http")
                            ? fileName
                            : $"{baseUrl}/Replica/Content/Uploads/Property/{property.place_id}/{Path.GetFileName(fileName)}")
                        .ToList();
                    string userImageUrl = userDetails != null
                        ? $"{baseUrl}/Replica/Content/Uploads/Images/{userDetails.image}"
                        : null;

                    var services = await _context.PlaceServices
                        .Where(ps => ps.place_id == property.place_id)
                        .Join(
                            _context.Services,
                            ps => ps.service_id,
                            s => s.service_id,
                            (ps, s) => s.service_name
                        )
                        .ToListAsync();


                    propertyDetails.Add(new NearbyPropertyDto
                    {
                        OwnerId = userDetails?.user_id ?? 0,
                        Name = userDetails?.name ?? "Unknown",
                        Image = userImageUrl,
                        PropertyId = property.place_id,
                        Title = property.title,
                        Price = property.price,
                        NoFloors = property.nofloors,
                        NoOfBeds = property.no_of_beds,
                        Rooms = property.rooms,
                        Bathroom = property.bathroom,
                        Kitchen = property.kitchen,
                        Description = property.description,
                        PlaceType = placeType?.placetype_name ?? "N/A",
                        PlaceSubtype = placeSubtype?.subtype_name ?? "N/A",
                        Photos = photos,
                        Services = services,
                        StartDate = startDate,
                        EndDate = endDate
                    });
                }


                return Request.CreateResponse(HttpStatusCode.OK, propertyDetails);
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, $"An error occurred: {ex.Message}");
            }
        }






        [HttpPut]
        public IHttpActionResult UpdatePrice(int pid, decimal upprice)
        {
            try
            {
                var place = _context.Places.FirstOrDefault(p => p.place_id == pid);
                if (place == null)
                {
                    return NotFound();
                }

                place.price = upprice;
                _context.SaveChanges();


                return Ok(new { Message = "Price updated successfully.", UpdatedPrice = upprice });
            }
            catch (Exception ex)
            {

                return InternalServerError(ex);
            }
        }

        
        [HttpPut]
        public IHttpActionResult GetHighestRatedProperty()
        {
            var highestRatedProperty = _context.Places
                .Join(_context.ReviewsAndRatings, 
                    place => place.place_id,    
                    rating => rating.property_reviewee_id, 
                    (place, rating) => new { place, rating })
                .GroupBy(pr => new
                {
                    pr.place.place_id,
                   
                    pr.place.latitude,
                    pr.place.longitude
                })
                .Select(group => new
                {
                    PropertyId = group.Key.place_id,
                  
                    Latitude = group.Key.latitude,
                    Longitude = group.Key.longitude,
                    AverageRating = group.Average(x => x.rating.property_rating) 
                })
                .OrderByDescending(p => p.AverageRating)
                .FirstOrDefault(); 

            if (highestRatedProperty == null)
                return NotFound(); 

            return Ok(highestRatedProperty); 
        }

        [HttpGet]
        public IHttpActionResult GetPropertiesByAscendingRating()
        {
            var propertiesByAscendingRating = _context.Places
                .Join(_context.ReviewsAndRatings,
                    place => place.place_id,
                    rating => rating.property_reviewee_id,
                    (place, rating) => new { place, rating })
                .GroupBy(pr => new
                {
                    pr.place.place_id,
                    pr.place.price,
                    pr.place.latitude,
                    pr.place.longitude
                })
                .Select(group => new
                {
                    PropertyId = group.Key.place_id,
                    Price = group.Key.price,
                    Latitude = group.Key.latitude,
                    Longitude = group.Key.longitude,
                    AverageRating = group.Average(x => x.rating.property_rating)
                })
                .OrderBy(p => p.AverageRating) 
                .ToList();

            return Ok(propertiesByAscendingRating);
        }


        [HttpGet]
        public IHttpActionResult GetPropertiesByDescendingRating()
        {
            var propertiesByDescendingRating = _context.Places
                .Join(_context.ReviewsAndRatings,
                    place => place.place_id,
                    rating => rating.property_reviewee_id,
                    (place, rating) => new { place, rating })
                .GroupBy(pr => new
                {
                    pr.place.place_id,
                    pr.place.price,
                    pr.place.latitude,
                    pr.place.longitude
                })
                .Select(group => new
                {
                    PropertyId = group.Key.place_id,
                    Price = group.Key.price,
                    Latitude = group.Key.latitude,
                    Longitude = group.Key.longitude,
                    AverageRating = group.Average(x => x.rating.property_rating)
                })
                .OrderByDescending(p => p.AverageRating) 
                .ToList();

            return Ok(propertiesByDescendingRating);
        }



        [HttpGet]
        public IHttpActionResult GetHighestRatedPropertyByUser(int userId)
        {
            var highestRatedPropertyByUser = _context.Places
                .Where(place => place.user_id == userId) 
                .Join(_context.ReviewsAndRatings,
                    place => place.place_id,
                    rating => rating.property_reviewee_id,
                    (place, rating) => new { place, rating })
                .GroupBy(pr => new
                {
                    pr.place.place_id,
                    pr.place.price,
                    pr.place.latitude,
                    pr.place.longitude,
                    pr.place.title 
                })
                .Select(group => new
                {
                    PropertyId = group.Key.place_id,
                    Price = group.Key.price,
                    Latitude = group.Key.latitude,
                    Longitude = group.Key.longitude,
                    Title = group.Key.title,
                    AverageRating = group.Average(x => x.rating.property_rating)
                })
                .OrderByDescending(p => p.AverageRating) 
                .FirstOrDefault(); 

            if (highestRatedPropertyByUser == null)
                return NotFound();

            return Ok(highestRatedPropertyByUser);
        }





    
        [HttpGet]
        public IHttpActionResult GetAllPropertiesByUserOrderedByRating(int userId)
        {
            var properties = _context.Places
                .Where(p => p.user_id == userId)
                .Select(p => new
                {
                    PropertyId = p.place_id,
                    Title = p.title,
                    AverageRating = _context.ReviewsAndRatings
                        .Where(r => r.property_reviewee_id == p.place_id)
                        .Average(r => r.property_rating) 
                })
                .OrderBy(p => p.AverageRating) 
                .ToList();

            if (!properties.Any())
                return NotFound();

            return Ok(properties);
        }


       


        [HttpGet]
        public IHttpActionResult GetHighestRatedUser()
        {
            var highestRatedUser = _context.Users
                .Select(u => new
                {
                    UserId = u.user_id,
                    UserName = u.name,
                    AverageRating = _context.ReviewsAndRatings
                        .Where(r => r.user_reviewee_id == u.user_id)
                        .Average(r => r.user_rating) 
                })
                .OrderByDescending(u => u.AverageRating)
                .FirstOrDefault();

            if (highestRatedUser == null)
                return NotFound();

            return Ok(highestRatedUser);
        }


        


        [HttpPost]
       
        public IHttpActionResult IncrementViewCount(int userId, int placeId)
        {
          
            
                var view =_context.ViewHistories.FirstOrDefault(v => v.user_id == userId && v.place_id == placeId);
                if (view == null)
                {
                    _context.ViewHistories.Add(new ViewHistory { user_id = userId, place_id = placeId, view_count = 1 });
                }
                else
                {
                    view.view_count += 1;
                }
                _context.SaveChanges();
                return Ok();
            
        }




        [HttpPost]
                public IHttpActionResult DecrementViewCount(int userId, int placeId)
        {
            
            
                var view = _context.ViewHistories.FirstOrDefault(v => v.user_id == userId && v.place_id == placeId);
                if (view != null && view.view_count > 0)
                {
                    view.view_count -= 1;
                    _context.SaveChanges();
                }
                return Ok();
            
        }



        [HttpGet]
        public IHttpActionResult GetViewCount(int placeId)
        {

            var count = _context.ViewHistories
                               .Where(v => v.place_id == placeId)
                               .Select(v => (int?)v.view_count)
                               .DefaultIfEmpty()
                               .Sum(v => v ?? 0);

            return Ok(count);
        }

        [HttpGet]
        public IHttpActionResult GetPropertiesRatedAboveThreeByUser(int userId)
        {
            var propertiesRatedAboveThree = _context.Places
                .Where(place => place.user_id == userId) 
                .Join(_context.ReviewsAndRatings,
                    place => place.place_id,
                    rating => rating.property_reviewee_id,
                    (place, rating) => new { place, rating })
                .Where(pr => pr.rating.property_rating > 3) 
                .Select(pr => new
                {
                    PropertyId = pr.place.place_id,
                    Title = pr.place.title,
                    Price = pr.place.price,
                    Latitude = pr.place.latitude,
                    Longitude = pr.place.longitude,
                    Rating = pr.rating.property_rating
                })
                .OrderByDescending(pr => pr.Rating) 
                .ToList(); 

            if (!propertiesRatedAboveThree.Any())
                return NotFound();

            return Ok(propertiesRatedAboveThree);
        }
       
        

        [HttpGet]
        public IHttpActionResult GetUsersWithRatingsAboveThree()
        {
            var usersWithHighRatings = _context.ReviewsAndRatings
                .Where(rating => rating.property_rating > 3) // Filter to include only ratings above 3
                .Join(_context.Users,
                    rating => rating.reviewer_id, // Use 'reviewer_id' to join
                    user => user.user_id,
                    (rating, user) => new { rating, user })
                .GroupBy(ru => new
                {
                    UserId = ru.user.user_id,
                    UserName = ru.user.name // Assuming there is a username field
                })
                .Select(group => new
                {
                    UserId = group.Key.UserId,
                    UserName = group.Key.UserName,
                    AverageRating = group.Average(x => x.rating.property_rating),
                    CountOfRatings = group.Count() // Counts the number of ratings above 3
                })
                .OrderByDescending(u => u.AverageRating) // Order by average rating in descending order
                .ToList(); // Convert the results into a list

            if (!usersWithHighRatings.Any())
                return NotFound();

            return Ok(usersWithHighRatings);
        }

        [HttpGet]
        public HttpResponseMessage GetPropertyReviewDetails(int propertyId)
        {
            try
            {
                string baseUrl = $"{Request.RequestUri.Scheme}://{Request.RequestUri.Host}:{Request.RequestUri.Port}";

                // Verify if the property exists
                var propertyExists = _context.Places.Any(p => p.place_id == propertyId);
                if (!propertyExists)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Property not found.");
                }

                // Fetch reviews for the specified property
                var reviewDetails = _context.ReviewsAndRatings
                    .Where(r => r.property_reviewee_id == propertyId)
                    .Select(r => new
                    {
                        PropertyId = r.property_reviewee_id,
                        ReviewerName = _context.Users.Where(u => u.user_id == r.reviewer_id).Select(u => u.name).FirstOrDefault(),
                        ReviewerImage = _context.Users.Where(u => u.user_id == r.reviewer_id).Select(u => u.image).FirstOrDefault(),
                        PropertyComment = r.property_comment
                    })
                    .ToList()
                    .Select(r => new
                    {
                        r.PropertyId,
                        r.ReviewerName,
                        ReviewerImage = r.ReviewerImage != null ? $"{baseUrl}/images/users/{r.ReviewerImage}" : null,
                        r.PropertyComment
                    }).ToList();

                if (!reviewDetails.Any())
                {
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, "No reviews found for this property.");
                }

                return Request.CreateResponse(HttpStatusCode.OK, reviewDetails);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }



        [HttpGet]
        public async Task<HttpResponseMessage> greGetPropertiesNearFamousPlace(string fm, decimal minPrice, decimal maxPrice, int noofrooms)
        {
            try
            {
                var famousPlace = _context.Nearbies.FirstOrDefault(fp => fp.typename == fm);

                if (famousPlace == null)
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "Famous place not found.");
                }

                double famousLatitude = Convert.ToDouble(famousPlace.latitude);
                double famousLongitude = Convert.ToDouble(famousPlace.longitude);

                string baseUrl = $"{Request.RequestUri.Scheme}://{Request.RequestUri.Host}";

               
                var validPlaceIds = _context.Bookings
                    .Where(b => b.booking_status != 1)
                    .Select(b => b.place_id)
                    .Concat(_context.CheckAvailabilities
                        .Where(a => a.status != 1)
                        .Select(a => a.place_id))
                    .Distinct();

                var properties = await _context.Places
                    .Where(p => p.price >= minPrice && p.price <= maxPrice && p.rooms >= noofrooms && validPlaceIds.Contains(p.place_id))
                    .Select(p => new
                    {
                        Property = p,
                        AverageRating = _context.ReviewsAndRatings
                                          .Where(r => r.property_reviewee_id == p.place_id)
                                          .Average(r => r.property_rating) ?? 0
                    })
                    .Where(p => p.AverageRating > 3) 
                    .OrderByDescending(p => p.AverageRating)
                    .ToListAsync();

                var nearbyProperties = new List<NearbyPropertyDto>();

                foreach (var prop in properties)
                {
                    double propertyLatitude = Convert.ToDouble(prop.Property.latitude);
                    double propertyLongitude = Convert.ToDouble(prop.Property.longitude);

                    double distance = CalculateDistance(
                        famousLatitude,
                        famousLongitude,
                        propertyLatitude,
                        propertyLongitude
                    );

                    if (distance <= 1.0) // Checking distance criteria
                    {
                        var placeType = await _context.Placetypes.FirstOrDefaultAsync(pt => pt.placetype_id == prop.Property.placetype_id);
                        var placeSubtype = await _context.PlaceSubtypes.FirstOrDefaultAsync(ps => ps.subtype_id == prop.Property.subtype_id);
                        var userdetails = await _context.Users.FirstOrDefaultAsync(u => u.user_id == prop.Property.user_id);

                        var photoFileNames = await _context.Photogalleries
                            .Where(pg => pg.place_id == prop.Property.place_id)
                            .Select(pg => pg.image)
                            .ToListAsync();

                        var services = await _context.PlaceServices
                            .Where(ps => ps.place_id == prop.Property.place_id)
                            .Join(_context.Services,
                                  ps => ps.service_id,
                                  s => s.service_id,
                                  (ps, s) => s.service_name)
                            .ToListAsync();

                        string userImageUrl = userdetails != null
                            ? $"{baseUrl}/Replica/Content/Uploads/Images/{userdetails.image}"
                            : null;
                        var photos = photoFileNames
                            .Select(fileName => fileName.StartsWith("http")
                                ? fileName
                                : $"{baseUrl}/Replica/Content/Uploads/Property/{prop.Property.place_id}/{Path.GetFileName(fileName)}").Distinct()
                            .ToList();

                        nearbyProperties.Add(new NearbyPropertyDto
                        {
                            OwnerId = userdetails.user_id,
                            Name = userdetails?.name,
                            Image = userImageUrl,
                            PropertyId = prop.Property.place_id,
                            Title = prop.Property.title,
                            Price = prop.Property.price,
                            NoFloors = prop.Property.nofloors,
                            NoOfBeds = prop.Property.no_of_beds,
                            Rooms = prop.Property.rooms,
                            Bathroom = prop.Property.bathroom,
                            Kitchen = prop.Property.kitchen,
                            Description = prop.Property.description,
                            DistanceFromFamousPlace = distance,
                            PlaceType = placeType?.placetype_name,
                            PlaceSubtype = placeSubtype?.subtype_name,
                            Photos = photos,
                            Services = services,
                            AverageRating = prop.AverageRating
                        });
                    }
                }
                if (nearbyProperties.Any())
                    return Request.CreateResponse(HttpStatusCode.OK, nearbyProperties);
                else
                    return Request.CreateResponse(HttpStatusCode.NotFound, "No properties found within the specified criteria.");
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, "An error occurred: " + ex.Message);
            }
        }


    }
}
















