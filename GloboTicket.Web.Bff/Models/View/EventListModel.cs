using System;
using System.Collections.Generic;

using GloboTicket.Gateway.Shared.Event;

namespace GloboTicket.Web.Models.View
{
    public class EventListModel
    {
        public IEnumerable<EventDto> Events { get; set; }
        public Guid SelectedCategory { get; set; }
        public IEnumerable<CategoryDto> Categories { get; set; }
        public int NumberOfItems { get; set; }
    }
}
