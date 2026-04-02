using System;
using System.Collections.Generic;
using GloboTicket.Services.ShoppingBasket.Entities;

namespace GloboTicket.Services.ShoppingBasket.Data
{
    public static class EventSeedData
    {
        public static List<Event> GetEvents()
        {
            //Use an anchor date so that all event start dates and times will match across the UI
            DateTime eventDate = new DateTime(2026, 03, 01, 19, 0, 0, DateTimeKind.Utc);

            return new List<Event>
            {
                new Event
                {
                    EventId = Guid.Parse("EE272F8B-6096-4CB6-8625-BB4BB2D89E8B"),
                    Name = "John Egbert Live",
                    Artist = "John Egbert",
                    Price = 65,
                    Date = eventDate.AddMonths(6).AddDays(14),
                    Description = "Join John for his farewell tour across 15 continents.",
                    ImageUrl = "https://gillcleerenpluralsight.blob.core.windows.net/files/GloboTicket/banjo.jpg",
                    VenueName = "Massey Hall",
                    VenueCity = "Toronto",
                    VenueCountry = "Canada",
                    Tickets = new List<Ticket>
                    {
                        new Ticket
                        {
                            TicketId = Guid.Parse("{CFB88E29-4744-48C0-94FA-B25B92DEA31A}"),
                            Name = "Standard",
                            Price = 65
                        },
                        new Ticket
                        {
                            TicketId = Guid.Parse("{CFB88E29-4744-48C0-94FA-B25B92DEA31B}"),
                            Name = "Premium",
                            Price = 95
                        }
                    }
                },
                new Event
                {
                    EventId = Guid.Parse("3448D5A4-0F72-4DD7-BF15-C14A46B26C00"),
                    Name = "The State of Affairs: Michael Live!",
                    Artist = "Michael Johnson",
                    Price = 85,
                    Date = eventDate.AddMonths(9),
                    Description = "Michael Johnson's 25 concerts across the globe last year were seen by thousands.",
                    ImageUrl = "https://gillcleerenpluralsight.blob.core.windows.net/files/GloboTicket/michael.jpg",
                    VenueName = "Massey Hall",
                    VenueCity = "Toronto",
                    VenueCountry = "Canada",
                    Tickets = new List<Ticket>
                    {
                        new Ticket
                        {
                            TicketId = Guid.Parse("{CFB88E29-4744-48C0-94FA-B25B92DEA31C}"),
                            Name = "Standard",
                            Price = 85
                        },
                        new Ticket
                        {
                            TicketId = Guid.Parse("{CFB88E29-4744-48C0-94FA-B25B92DEA31D}"),
                            Name = "Premium",
                            Price = 110
                        }
                    }
                },
                new Event
                {
                    EventId = Guid.Parse("B419A7CA-3321-4F38-BE8E-4D7B6A529319"),
                    Name = "Clash of the DJs",
                    Artist = "DJ 'The Mike'",
                    Price = 85,
                    Date = eventDate.AddMonths(4).AddDays(20),
                    Description = "DJs from all over the world will compete in this epic battle for eternal fame.",
                    ImageUrl = "https://gillcleerenpluralsight.blob.core.windows.net/files/GloboTicket/dj.jpg",
                    VenueName = "L'Olympia",
                    VenueCity = "Montreal",
                    VenueCountry = "Canada",
                    Tickets = new List<Ticket>
                    {
                        new Ticket
                        {
                            TicketId = Guid.Parse("{CFB88E29-4744-48C0-94FA-B25B92DEA32A}"),
                            Name = "Standard",
                            Price = 85
                        }
                    }
                },
                new Event
                {
                    EventId = Guid.Parse("62787623-4C52-43FE-B0C9-B7044FB5929B"),
                    Name = "Spanish guitar hits with Manuel",
                    Artist = "Manuel Santinonisi",
                    Price = 25,
                    Date = eventDate.AddMonths(4).AddDays(5),
                    Description = "Get on the hype of Spanish Guitar concerts with Manuel.",
                    ImageUrl = "https://gillcleerenpluralsight.blob.core.windows.net/files/GloboTicket/guitar.jpg",
                    VenueName = "L'Olympia",
                    VenueCity = "Montreal",
                    VenueCountry = "Canada",
                    Tickets = new List<Ticket>
                    {
                        new Ticket
                        {
                            TicketId = Guid.Parse("{CFB88E29-4744-48C0-94FA-B25B92DEA32B}"),
                            Name = "Standard",
                            Price = 25
                        }
                    }
                },
                new Event
                {
                    EventId = Guid.Parse("1BABD057-E980-4CB3-9CD2-7FDD9E525668"),
                    Name = "Techorama 2026",
                    Artist = "Many",
                    Price = 400,
                    Date = eventDate.AddMonths(10).AddDays(14),
                    Description = "The best tech conference in the world",
                    ImageUrl = "https://gillcleerenpluralsight.blob.core.windows.net/files/GloboTicket/conf.jpg",
                    VenueName = "Commodore Ballroom",
                    VenueCity = "Vancouver",
                    VenueCountry = "Canada",
                    Tickets = new List<Ticket>
                    {
                        new Ticket
                        {
                            TicketId = Guid.Parse("{CFB88E29-4744-48C0-94FA-B25B92DEA32C}"),
                            Name = "Standard",
                            Price = 400
                        }
                    }
                },
                new Event
                {
                    EventId = Guid.Parse("ADC42C09-08C1-4D2C-9F96-2D15BB1AF299"),
                    Name = "To the Moon and Back",
                    Artist = "Nick Sailor",
                    Price = 135,
                    Date = eventDate.AddMonths(8),
                    Description = "A sing and dance extravaganza written by Nick Sailor.",
                    ImageUrl = "https://gillcleerenpluralsight.blob.core.windows.net/files/GloboTicket/musical.jpg",
                    VenueName = "Commodore Ballroom",
                    VenueCity = "Vancouver",
                    VenueCountry = "Canada",
                    Tickets = new List<Ticket>
                    {
                        new Ticket
                        {
                            TicketId = Guid.Parse("{CFB88E29-4744-48C0-94FA-B25B92DEA31E}"),
                            Name = "Standard",
                            Price = 135
                        },
                        new Ticket
                        {
                            TicketId = Guid.Parse("{CFB88E29-4744-48C0-94FA-B25B92DEA31F}"),
                            Name = "Premium",
                            Price = 190
                        }
                    }
                }
            };
        }
    }
}