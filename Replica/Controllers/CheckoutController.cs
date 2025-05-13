using Replica.Models;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;

namespace Replica.Controllers
{
    public class CheckoutController : ApiController
    {
        private readonly ReplicaAirbnbEntities1 _context;

        public CheckoutController()
        {
            _context = new ReplicaAirbnbEntities1();
        }

        public class CheckoutDTO
        {
            public int CheckoutId { get; set; }
            public int BookingId { get; set; }
            public int UserId { get; set; }
            public DateTime CheckinDate { get; set; }
            public DateTime CheckoutDate { get; set; }
            public int? CheckoutStatus { get; set; }
        }

        [HttpPost]
        public async Task<HttpResponseMessage> AddCheckout([FromBody] CheckoutDTO checkoutDTO)
        {
           
            using (var transaction = _context.Database.BeginTransaction())
            {
                try
                {
                    var checkout = new Checkout
                    {
                        booking_id = checkoutDTO.BookingId,
                        user_id = checkoutDTO.UserId,
                        checkin_date = checkoutDTO.CheckinDate,
                        checkout_date = checkoutDTO.CheckoutDate,
                        checkout_status = checkoutDTO.CheckoutStatus
                    };

                    _context.Checkouts.Add(checkout);

           
                    var booking = await _context.Bookings.FindAsync(checkoutDTO.BookingId);
                    if (booking == null)
                    {
                        return Request.CreateResponse(HttpStatusCode.NotFound, "Booking not found.");
                    }

                    
                    booking.booking_status = 2;

                   
                    await _context.SaveChangesAsync();

                  
                    transaction.Commit();

                    return Request.CreateResponse(HttpStatusCode.OK);
                }
                catch (Exception ex)
                {
                    
                    transaction.Rollback();
                    return Request.CreateResponse(HttpStatusCode.InternalServerError, $"An error occurred: {ex.Message}");
                }
            }
        }




        [HttpPost]
        public IHttpActionResult UpdateCheckoutStatus(int bookingId, int status)
        {
            var booking = _context.Checkouts.FirstOrDefault(c=>c.booking_id == bookingId);
            if (booking == null)
            {
                return NotFound();
            }


            booking.checkout_status = status;
            _context.SaveChanges();

            return Ok();
        }


    }
}
