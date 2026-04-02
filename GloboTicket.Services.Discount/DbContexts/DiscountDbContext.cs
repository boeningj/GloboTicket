using GloboTicket.Services.Discount.Entities;
using Microsoft.EntityFrameworkCore;
using System;

namespace GloboTicket.Services.Discount.DbContexts
{
    // public class DiscountDbContext: DbContext
    // {
    //     public DiscountDbContext(DbContextOptions<DiscountDbContext> options) : base(options)
    //     {

    //     }

    //     public DbSet<Coupon> Coupons { get; set; }

    //     protected override void OnModelCreating(ModelBuilder modelBuilder)
    //     {
    //         base.OnModelCreating(modelBuilder);


    //         modelBuilder.Entity<Coupon>().HasData(new Coupon
    //         {
    //             //CouponId = Guid.NewGuid(),
    //             CouponId = new Guid("53447bb6-8545-4799-95dd-fdf75d2afa50"),
    //             Code = "BeNice",
    //             Amount = 10,
    //             AlreadyUsed = false
    //         });

    //         modelBuilder.Entity<Coupon>().HasData(new Coupon
    //         {
    //             //CouponId = Guid.NewGuid(),
    //             CouponId = new Guid("53447bb6-8545-4799-95dd-fdf75d2afa51"),
    //             Code = "Awesome",
    //             Amount = 20,
    //             AlreadyUsed = false
    //         });

    //         modelBuilder.Entity<Coupon>().HasData(new Coupon
    //         {
    //             //CouponId = Guid.NewGuid(),
    //             CouponId = new Guid("53447bb6-8545-4799-95dd-fdf75d2afa52"),
    //             Code = "AlmostFree",
    //             Amount = 100,
    //             AlreadyUsed = false
    //         });

    //     }
    // }

    public class DiscountDbContext: DbContext
    {
        public DiscountDbContext(DbContextOptions<DiscountDbContext> options) : base(options)
        {

        }

        public DbSet<Coupon> Coupons { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Coupon>().HasData(new Coupon
            {
                CouponId = Guid.Parse("53447bb6-8545-4799-95dd-fdf75d2afa50"), 
                Amount = 10, 
                UserId = Guid.Parse("{E455A3DF-7FA5-47E0-8435-179B300D531F}")
            });

            modelBuilder.Entity<Coupon>().HasData(new Coupon
            {
                CouponId = Guid.Parse("53447bb6-8545-4799-95dd-fdf75d2afa51"), 
                Amount = 20, 
                UserId = Guid.Parse("{bbf594b0-3761-4a65-b04c-eec4836d9070}")
            }); 
        }
    }
}
