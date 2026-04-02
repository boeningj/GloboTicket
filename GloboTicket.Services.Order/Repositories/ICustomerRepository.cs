using GloboTicket.Services.Ordering.Entities;
using System;
using System.Threading.Tasks;

namespace GloboTicket.Services.Ordering.Repositories
{
    public interface ICustomerRepository
    {
        Task<Customer> GetCustomerById(Guid customerId);
        Task AddCustomer(Customer customer);

    }
}