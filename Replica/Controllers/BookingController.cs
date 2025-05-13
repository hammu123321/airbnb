using Replica;
using Replica.Models;
using System;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;

public class BookingController : ApiController
{
    private readonly ReplicaAirbnbEntities1 _context;

    public BookingController()
    {
        _context = new ReplicaAirbnbEntities1();
    }

   
    public class ReviewAndRatingDTO
    {
        public int BookingId { get; set; }
        public int ReviewerId { get; set; }
        public int? UserRevieweeId { get; set; }
        public int? PropertyRevieweeId { get; set; }
        public decimal? UserRating { get; set; }
        public decimal? PropertyRating { get; set; }
        public string UserComment { get; set; }
        public string PropertyComment { get; set; }
    }
    public class AvailabilityDTO
    {

        public int PlaceId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int Status { get; set; } 
    }
    public class BookingDTO
    {
        public int UserId { get; set; }
        public int PlaceId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int NoOfAdults { get; set; }
        public int? NoOfChildren { get; set; }
        public int? NoOfInfants { get; set; }
        public bool PetsAllowed { get; set; }
        public decimal TotalCost { get; set; }
        public int tokens { get; set; }

    }





    [HttpPost]
    public Task<HttpResponseMessage> CreateBooking([FromBody] BookingDTO bookingDto)
    {
        if (bookingDto == null)
        {
            return Task.FromResult(Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Booking data isrequired."));
        }

        if (bookingDto.StartDate >= bookingDto.EndDate)
        {
            return Task.FromResult(Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Start date must be earlierthan the end date."));
        }
        var users = _context.Bookings
        .Join(_context.Users,
            b => new { b.user_id },
            user => new { user.user_id },
            (b, user) => new { b, user })
        .FirstOrDefault();

        if (users == null)
        {
            return Task.FromResult(Request.CreateErrorResponse(HttpStatusCode.NotFound, "User not found."));
        }

        var gender = users.user.gender;
        
        var booking = new Booking
        {
            user_id = bookingDto.UserId,
            place_id = bookingDto.PlaceId,
            start_date = bookingDto.StartDate,
            end_date = bookingDto.EndDate,
            no_of_adults = bookingDto.NoOfAdults,
            no_of_children = bookingDto.NoOfChildren ?? 0,
            no_of_infants = bookingDto.NoOfInfants ?? 0,
            pets_allowed = bookingDto.PetsAllowed,
            total_cost = bookingDto.TotalCost,
            booking_status = 0,
            tokens=bookingDto.tokens,
            
        };
        try
        {
            _context.Bookings.Add(booking);
            _context.SaveChanges();
            return Task.FromResult(Request.CreateResponse(HttpStatusCode.Created, new
            {
                bookingId = booking.booking_id
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message));
        }
    }

    [HttpGet]
    public IHttpActionResult Gettokencountt(int userId)
    {
       
        var count = _context.Bookings
                           .Where(b=>b.user_id==userId)
                           .Select(v => (int?)v.tokens) 
                           .DefaultIfEmpty()
                           .Sum(v => v ?? 0);

        return Ok(count);
    }



    [HttpPost]

    public async Task<IHttpActionResult> updatetoken(int uid, int token)
    {
        var entries = await _context.Bookings
                               .Where(b=>b.user_id==uid)
                               .ToListAsync();

        if (entries == null || !entries.Any())
        {
            return NotFound();
        }

        foreach (var entry in entries)
        {
            entry.tokens = token;
        }

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            return InternalServerError(ex);
        }

        return Ok("Status updated successfully");
    }

    [HttpGet]
    public IHttpActionResult GetBookingStatus(int bookingId)
    {
        var booking = _context.Bookings.FirstOrDefault(b => b.booking_id == bookingId);
        if (booking == null)
            return NotFound();

        return Ok(new { booking_id = booking.booking_id, booking_status = booking.booking_status, place_id=booking.place_id }); ;
    }



    [HttpGet]
    public IHttpActionResult GetProperptyStatus(int perptyId)
    {
        var booking = _context.Bookings.Where(b => b.place_id == perptyId && b.booking_status==1).FirstOrDefault();

        if (booking == null)
            return Ok(new { booking_id = 0, booking_status = 0, place_id = 0 });

        return Ok(new { booking_id = booking.booking_id, booking_status = booking.booking_status, place_id = booking.place_id }); 
    }



    [HttpPost]
    public IHttpActionResult UpdateBookingStatus(int bookingId, int status)
    {
        var booking = _context.Bookings.FirstOrDefault(b => b.booking_id == bookingId);
        if (booking == null)
        {
            return NotFound();
        }


        booking.booking_status = status;
        _context.SaveChanges();

        return Ok();
    }


    [HttpGet]
    public IHttpActionResult GetPendingBookings(int hostId)
    {
        string baseUrl = $"{Request.RequestUri.Scheme}://{Request.RequestUri.Host}:{Request.RequestUri.Port}";

        var pendingBookingsData = _context.Bookings
            .Join(_context.Places,
                  booking => booking.place_id,
                  place => place.place_id,
                  (booking, place) => new { booking, place })
            .Join(_context.Users,
                  bp => bp.booking.user_id,
                  user => user.user_id,
                  (bp, user) => new { bp.booking, bp.place, user })
            .Where(bpu => bpu.place.user_id == hostId && bpu.booking.booking_status == 0)
            .Select(bpu => new
            {
                BookingId = bpu.booking.booking_id,
                ClientId = bpu.booking.user_id,
                ClientName = bpu.user.name,
                ClientImage = bpu.user.image,  // Get the raw image name only
                PlaceId = bpu.booking.place_id,
                StartDate = bpu.booking.start_date,
                EndDate = bpu.booking.end_date,
                NoOfAdults = bpu.booking.no_of_adults,
                NoOfChildren = bpu.booking.no_of_children,
                NoOfInfants = bpu.booking.no_of_infants,
                PetsAllowed = bpu.booking.pets_allowed,
                TotalCost = bpu.booking.total_cost,
                BookingStatus = bpu.booking.booking_status
            }).ToList();

        // Add the base URL after fetching the data
        var pendingBookings = pendingBookingsData.Select(b => new
        {
            b.BookingId,
            b.ClientId,
            b.ClientName,
            ClientImageUrl = b.ClientImage != null
                ? $"{baseUrl}/Replica/Content/Uploads/Images/{b.ClientImage}"
                : null,
            b.PlaceId,
            b.StartDate,
            b.EndDate,
            b.NoOfAdults,
            b.NoOfChildren,
            b.NoOfInfants,
            b.PetsAllowed,
            b.TotalCost,
            b.BookingStatus
        }).ToList();

        if (!pendingBookings.Any())
        {
            return Content(HttpStatusCode.NotFound, "No pending bookings found.");
        }

        return Ok(pendingBookings);
    }


    [HttpGet]
    public IHttpActionResult GetActiveBookings(int userId)
    {
        string baseUrl = $"{Request.RequestUri.Scheme}://{Request.RequestUri.Host}:{Request.RequestUri.Port}";

        var activeBookings = _context.Bookings
                                     .Join(_context.Places,
                                           booking => booking.place_id,
                                           place => place.place_id,
                                           (booking, place) => new { booking, place })
                                     .Join(_context.Users, 
                                           place => place.place.user_id,
                                           owner => owner.user_id,
                                           (bp, owner) => new { bp.booking, bp.place, owner })
                                     .Where(bpo => bpo.booking.user_id == userId && bpo.booking.booking_status == 1)
                                     .Select(bpo => new
                                     {
                                         BookingId = bpo.booking.booking_id,
                                         ClientId = bpo.booking.user_id,
                                         PlaceId = bpo.booking.place_id,
                                         OwnerId = bpo.place.user_id,
                                         OwnerName = bpo.owner.name,
                                         OwnerPicture = bpo.owner.image, 
                                         StartDate = bpo.booking.start_date,
                                         EndDate = bpo.booking.end_date,
                                         NoOfAdults = bpo.booking.no_of_adults,
                                         NoOfChildren = bpo.booking.no_of_children,
                                         NoOfInfants = bpo.booking.no_of_infants,
                                         PetsAllowed = bpo.booking.pets_allowed,
                                         TotalCost = bpo.booking.total_cost,
                                         BookingStatus = bpo.booking.booking_status,
                                     }).ToList();

        
        var activeBookingsWithUrls = activeBookings.Select(b => new
        {
            b.BookingId,
            b.ClientId,
            b.PlaceId,
            b.OwnerId,
            b.OwnerName,
            OwnerImageUrl = b.OwnerPicture != null
                ? $"{baseUrl}/Replica/Content/Uploads/Images/{b.OwnerPicture}"
                : null,
            b.StartDate,
            b.EndDate,
            b.NoOfAdults,
            b.NoOfChildren,
            b.NoOfInfants,
            b.PetsAllowed,
            b.TotalCost,
            b.BookingStatus
        }).ToList();

        if (!activeBookingsWithUrls.Any())
        {
            return Content(HttpStatusCode.NotFound, "No active bookings found.");
        }

        return Ok(activeBookingsWithUrls);
    }


    [HttpGet]
    public IHttpActionResult LeftClientBookings(int ownerId)
    {
        var completedBookings = _context.Bookings
                                        .Join(_context.Places,
                                              booking => booking.place_id,
                                              place => place.place_id,
                                              (booking, place) => new { booking, place })
                                        .Join(_context.Checkouts,
                                              bp => bp.booking.booking_id,
                                              checkout => checkout.booking_id,
                                              (bp, checkout) => new { bp, checkout })
                                        .Where(bpc => bpc.bp.place.user_id == ownerId && bpc.checkout.checkout_status == 2) // Assuming 1 means checked out
                                        .Select(bpc => new
                                        {
                                            BookingId = bpc.bp.booking.booking_id,
                                            ClientId = bpc.checkout.user_id, // This is the client who booked and checked out
                                            PlaceId = bpc.bp.place.place_id,
                                            OwnerId = bpc.bp.place.user_id,
                                            StartDate = bpc.bp.booking.start_date,
                                            EndDate = bpc.bp.booking.end_date,
                                            CheckinDate = bpc.checkout.checkin_date,
                                            CheckoutDate = bpc.checkout.checkout_date,
                                            NoOfAdults = bpc.bp.booking.no_of_adults,
                                            NoOfChildren = bpc.bp.booking.no_of_children,
                                            NoOfInfants = bpc.bp.booking.no_of_infants,
                                            PetsAllowed = bpc.bp.booking.pets_allowed,
                                            TotalCost = bpc.bp.booking.total_cost,
                                            CheckoutStatus = bpc.checkout.checkout_status 
                                        }).ToList();

        if (!completedBookings.Any())
        {
            return Content(HttpStatusCode.NotFound, "No completed bookings found for the properties you own.");
        }

        return Ok(completedBookings);
    }






    [HttpPost]
    public Task<HttpResponseMessage> CreateReviewAndRating([FromBody] ReviewAndRatingDTO reviewDto)
    {
        if (reviewDto == null)
        {
            return Task.FromResult(Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Review data is required."));
        }

        var review = new ReviewsAndRating
        {
            booking_id = reviewDto.BookingId,
            reviewer_id = reviewDto.ReviewerId,
            user_reviewee_id = reviewDto.UserRevieweeId,
            property_reviewee_id = reviewDto.PropertyRevieweeId,
            user_rating = reviewDto.UserRating,
            property_rating = reviewDto.PropertyRating,
            user_comment = reviewDto.UserComment,
            property_comment = reviewDto.PropertyComment

        };
        try
        {
            _context.ReviewsAndRatings.Add(review);
            _context.SaveChanges();
            return Task.FromResult(Request.CreateResponse(HttpStatusCode.Created));
        }

        catch (Exception ex)
        {
            return Task.FromResult(Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message));
        }
    }


    [HttpGet]
    public HttpResponseMessage GetReviewsAboutUser(int userId)
    {
        try
        {
            string baseUrl = $"{Request.RequestUri.Scheme}://{Request.RequestUri.Host}:{Request.RequestUri.Port}";

            
            var user = _context.Users.FirstOrDefault(u => u.user_id == userId);
            if (user == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "User not found.");
            }

            
            string userImageUrl = user.image != null
                ? $"{baseUrl}/Replica/Content/Uploads/Images/{user.image}"
                : null;

            var reviewsData = _context.ReviewsAndRatings
                .Where(r => r.user_reviewee_id == user.user_id)
                .Select(r => new
                {
                    r.review_id,
                    r.reviewer_id,
                    ReviewerName = _context.Users.FirstOrDefault(u => u.user_id == r.reviewer_id).name,
                    ReviewerImageName = _context.Users.FirstOrDefault(u => u.user_id == r.reviewer_id).image,
                    r.user_rating,
                    r.user_comment,
                    r.property_rating,
                    r.property_comment
                }).ToList();

          
            double averageRating = reviewsData.Any() ? reviewsData.Average(r => (double)r.user_rating) : 0.0;
            var reviews = reviewsData.Select(r => new
            {
                r.review_id,
                r.reviewer_id,
                r.ReviewerName,
                ReviewerImage = r.ReviewerImageName != null
                    ? $"{baseUrl}/Replica/Content/Uploads/Images/{r.ReviewerImageName}"
                    : null,
                r.user_rating,
                r.user_comment,
                r.property_rating,
                r.property_comment
            }).ToList();

            var response = new
            {
                user_id = user.user_id,
                user_name = user.name,
                user_image = userImageUrl,
                reviews = reviews,
                average_user_rating = averageRating  
            };

            return Request.CreateResponse(HttpStatusCode.OK, response);
        }
        catch (Exception ex)
        {
            return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
        }
    }


    [HttpGet]
    public IHttpActionResult GetUserBookingHistory(int userId)
    {
        string baseUrl = $"{Request.RequestUri.Scheme}://{Request.RequestUri.Host}:{Request.RequestUri.Port}";

      
        var bookingHistoryData = _context.Bookings
            .Join(_context.Places,
                  booking => booking.place_id,
                  place => place.place_id,
                  (booking, place) => new { booking, place })
            .Join(_context.Users,
                  bp => bp.booking.user_id,
                  user => user.user_id,
                  (bp, user) => new { bp.booking, bp.place, user })
            .Join(_context.Checkouts,
                  bpu => bpu.booking.booking_id,
                  checkout => checkout.booking_id,
                  (bpu, checkout) => new { bpu, checkout })
            .Where(bpc => bpc.checkout.user_id == userId && bpc.checkout.checkout_status == 2)
            .Select(bpc => new
            {
                BookingId = bpc.bpu.booking.booking_id,
                HostId = bpc.bpu.place.user_id, 
                HostName = bpc.bpu.place.User.name, 
                HostImage = bpc.bpu.place.User.image, 
                PlaceId = bpc.bpu.booking.place_id,
                StartDate = bpc.bpu.booking.start_date,
                EndDate = bpc.bpu.booking.end_date,
                NoOfAdults = bpc.bpu.booking.no_of_adults,
                NoOfChildren = bpc.bpu.booking.no_of_children,
                NoOfInfants = bpc.bpu.booking.no_of_infants,
                PetsAllowed = bpc.bpu.booking.pets_allowed,
                TotalCost = bpc.bpu.booking.total_cost,
                CheckoutStatus = bpc.checkout.checkout_status  
            }).ToList();

       
        var bookingHistory = bookingHistoryData.Select(b => new
        {
            b.BookingId,
            b.HostId,
            b.HostName,
            HostImage = b.HostImage.StartsWith("http")
                        ? b.HostImage
                        : $"{baseUrl}/Replica/Content/Uploads/Images/{b.HostImage}",
            b.PlaceId,
            b.StartDate,
            b.EndDate,
            b.NoOfAdults,
            b.NoOfChildren,
            b.NoOfInfants,
            b.PetsAllowed,
            b.TotalCost,
            b.CheckoutStatus
        }).ToList();

        if (!bookingHistory.Any())
        {
            return Content(HttpStatusCode.NotFound, "No booking history found.");
        }

        return Ok(bookingHistory);
    }


    [HttpGet]
    public IHttpActionResult Gethostwithstatuscodetwolaterapplyrating(int hostId)
    {
        string baseUrl = $"{Request.RequestUri.Scheme}://{Request.RequestUri.Host}:{Request.RequestUri.Port}";

        var bookingHistoryData = _context.Bookings
            .Join(_context.Places,
                  booking => booking.place_id,
                  place => place.place_id,
                  (booking, place) => new { booking, place })
            .Join(_context.Users,
                  bp => bp.booking.user_id,
                  user => user.user_id,
                  (bp, user) => new { bp.booking, bp.place, user })
            .Where(bpu => bpu.place.user_id == hostId && bpu.booking.booking_status == 2)
            .Select(bpu => new
            {
                BookingId = bpu.booking.booking_id,
                HostId = bpu.place.user_id,  
                HostName = bpu.place.User.name,
                PlaceId = bpu.booking.place_id,
                StartDate = bpu.booking.start_date,
                EndDate = bpu.booking.end_date,
                NoOfAdults = bpu.booking.no_of_adults,
                NoOfChildren = bpu.booking.no_of_children,
                NoOfInfants = bpu.booking.no_of_infants,
                PetsAllowed = bpu.booking.pets_allowed,
                TotalCost = bpu.booking.total_cost,
                BookingStatus = bpu.booking.booking_status
            }).ToList();

        var bookingHistory = bookingHistoryData.Select(b => new
        {
            b.BookingId,
            b.HostId,
            b.HostName,
            b.PlaceId,
            b.StartDate,
            b.EndDate,
            b.NoOfAdults,
            b.NoOfChildren,
            b.NoOfInfants,
            b.PetsAllowed,
            b.TotalCost,
            b.BookingStatus
        }).ToList();

        if (!bookingHistory.Any())
        {
            return Content(HttpStatusCode.NotFound, "No booking history found.");
        }

        return Ok(bookingHistory);
    }


    



   [HttpPost]
public async Task<HttpResponseMessage> CreateAvailability([FromBody] AvailabilityDTO availabilityDto)
{
    if (availabilityDto == null)
    {
        return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Availability data is required.");
    }

    if (availabilityDto.StartDate >= availabilityDto.EndDate)
    {
        return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Start date must be earlier than the end date.");
    }

    try
    {
       
        bool exists = _context.CheckAvailabilities.Any(a =>
            a.place_id == availabilityDto.PlaceId &&
            a.status == 1 &&
            a.start_date == availabilityDto.StartDate &&
            a.end_date == availabilityDto.EndDate);

        if (exists)
        {
            return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Availability already disabled for the specified date range.");
        }

        var availability = new CheckAvailability
        {
            place_id = availabilityDto.PlaceId,
            start_date = availabilityDto.StartDate,
            end_date = availabilityDto.EndDate,
            status = 1 
        };

        _context.CheckAvailabilities.Add(availability);
        await _context.SaveChangesAsync();

        return Request.CreateResponse(HttpStatusCode.OK, "Availability created successfully.");
    }
    catch (Exception ex)
    {
        return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "An error occurred while creating the availability: " + ex.Message);
    }
}







    [HttpPost]
  
    public async Task<IHttpActionResult> UpdatePropertyStatus(int placeId, int newStatus)
    {
        var entries = await _context.CheckAvailabilities
                               .Where(c => c.place_id == placeId)
                               .ToListAsync();

        if (entries == null || !entries.Any())
        {
            return NotFound();
        }

        foreach (var entry in entries)
        {
            entry.status = newStatus;
        }

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            return InternalServerError(ex);
        }

        return Ok("Status updated successfully");
    }



[HttpGet]
    public IHttpActionResult ClientSidePendingBookingsRequestForRejection(int clientID)
    {
        string baseUrl = $"{Request.RequestUri.Scheme}://{Request.RequestUri.Host}:{Request.RequestUri.Port}";

        var pendingBookingsData = _context.Bookings
            .Join(_context.Places,
                  booking => booking.place_id,
                  place => place.place_id,
                  (booking, place) => new { booking, place })
            .Join(_context.Users,
                  bp => bp.booking.user_id,
                  user => user.user_id,
                  (bp, user) => new { bp.booking, bp.place, user })
            .Where(bpu => bpu.booking.user_id == clientID && bpu.booking.booking_status == 0)
            .Select(bpu => new
            {
                BookingId = bpu.booking.booking_id,
                ClientId = bpu.booking.user_id,
                ClientName = bpu.user.name,
                ClientImage = bpu.user.image,  
                PlaceId = bpu.booking.place_id,
                StartDate = bpu.booking.start_date,
                EndDate = bpu.booking.end_date,
                NoOfAdults = bpu.booking.no_of_adults,
                NoOfChildren = bpu.booking.no_of_children,
                NoOfInfants = bpu.booking.no_of_infants,
                PetsAllowed = bpu.booking.pets_allowed,
                TotalCost = bpu.booking.total_cost,
                BookingStatus = bpu.booking.booking_status
            }).ToList();

        var pendingBookings = pendingBookingsData.Select(b => new
        {
            b.BookingId,
            b.ClientId,
            b.ClientName,
            ClientImageUrl = b.ClientImage != null
                ? $"{baseUrl}/Replica/Content/Uploads/Images/{b.ClientImage}"
                : null,
            b.PlaceId,
            b.StartDate,
            b.EndDate,
            b.NoOfAdults,
            b.NoOfChildren,
            b.NoOfInfants,
            b.PetsAllowed,
            b.TotalCost,
            b.BookingStatus
        }).ToList();

        if (!pendingBookings.Any())
        {
            return Content(HttpStatusCode.NotFound, "No pending bookings found.");
        }

        return Ok(pendingBookings);
    }

    [HttpDelete]
    public IHttpActionResult DeletePlace(int placeId)
    {
        var place = _context.Places.Find(placeId);
        if (place == null)
        {
            return NotFound(); 
        }

      
        var activeBookings = _context.Bookings.Any(b => b.place_id == placeId && b.booking_status == 1);
        if (activeBookings)
        {
            return BadRequest("Cannot delete the place because there are active bookings.");
        }

       
        _context.Places.Remove(place);
        _context.SaveChanges();
        return Ok(); 
    }

}
