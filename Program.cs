using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using InterviewPortal.DbContexts;
using InterviewPortal.Services;
namespace InterviewPortal
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            var connectionString = builder.Configuration.GetConnectionString("InterviewPortalDbContextConnection") ?? throw new InvalidOperationException("Connection string 'InterviewPortalDbContextConnection' not found.");;

            builder.Services.AddDbContext<InterviewPortalDbContext>(options => options.UseSqlServer(connectionString));

            builder.Services.AddIdentity<User, IdentityRole>(options =>
    options.SignIn.RequireConfirmedAccount = false)
    .AddEntityFrameworkStores<InterviewPortalDbContext>()
    .AddDefaultTokenProviders();

            // Add services to the container.
            builder.Services.AddControllersWithViews();

            // Register the DatabaseInit
            builder.Services.AddHostedService<DatabaseInit>();

            builder.Services.AddRazorPages();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseRouting();

            app.UseStaticFiles();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapStaticAssets();
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}")
                .WithStaticAssets();

            app.MapRazorPages();

            app.Run();
        }
    }
}
