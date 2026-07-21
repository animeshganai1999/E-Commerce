using ECommerceBackend.Domain.Entities;
using ECommerceBackend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceBackend.Infrastructure.Data.Config
{
    public class ProductConfig : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> builder)
        {
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id)
                .ValueGeneratedNever(); // Id matches the external product id; not auto-generated

            builder.Property(p => p.Title).IsRequired().HasMaxLength(300);
            builder.Property(p => p.Price).IsRequired().HasPrecision(18, 2);
            builder.Property(p => p.Description).HasMaxLength(2000);
            builder.Property(p => p.Category).HasMaxLength(100);
            builder.Property(p => p.Image).HasMaxLength(500);
            builder.Property(p => p.StockQuantity).IsRequired();

            // Index the category — catalog pages filter/sort by it over lakhs of rows.
            builder.HasIndex(p => p.Category);

            // Seed the catalog to mirror the external (fakestoreapi) products the frontend uses.
            // Id matches the external product id so CartItem.ProductId maps to a real stock row.
            // Each product starts with an initial stock of 100 units.
            builder.HasData(
                new Product { Id = 1, Title = "Fjallraven - Foldsack No. 1 Backpack, Fits 15 Laptops", Price = 109.95m, Description = "Your perfect pack for everyday use and walks in the forest. Stash your laptop (up to 15 inches) in the padded sleeve, your everyday", Category = "men's clothing", Image = "https://fakestoreapi.com/img/81fPKd-2AYL._AC_SL1500_t.png", RatingRate = 3.9, RatingCount = 120, StockQuantity = 100 },
                new Product { Id = 2, Title = "Mens Casual Premium Slim Fit T-Shirts", Price = 22.30m, Description = "Slim-fitting style, contrast raglan long sleeve, three-button henley placket, light weight & soft fabric for breathable and comfortable wearing.", Category = "men's clothing", Image = "https://fakestoreapi.com/img/71-3HjGNDUL._AC_SY879._SX._UX._SY._UY_t.png", RatingRate = 4.1, RatingCount = 259, StockQuantity = 100 },
                new Product { Id = 3, Title = "Mens Cotton Jacket", Price = 55.99m, Description = "great outerwear jackets for Spring/Autumn/Winter, suitable for many occasions, such as working, hiking, camping, mountain/rock climbing, cycling, traveling or other outdoors.", Category = "men's clothing", Image = "https://fakestoreapi.com/img/71li-ujtlUL._AC_UX679_t.png", RatingRate = 4.7, RatingCount = 500, StockQuantity = 100 },
                new Product { Id = 4, Title = "Mens Casual Slim Fit", Price = 15.99m, Description = "The color could be slightly different between on the screen and in practice. Please note that body builds vary by person, therefore, detailed size information should be reviewed below on the product description.", Category = "men's clothing", Image = "https://fakestoreapi.com/img/71YXzeOuslL._AC_UY879_t.png", RatingRate = 2.1, RatingCount = 430, StockQuantity = 100 },
                new Product { Id = 5, Title = "John Hardy Women's Legends Naga Gold & Silver Dragon Station Chain Bracelet", Price = 695m, Description = "From our Legends Collection, the Naga was inspired by the mythical water dragon that protects the ocean's pearl. Wear facing inward to be bestowed with love and abundance, or outward for protection.", Category = "jewelery", Image = "https://fakestoreapi.com/img/71pWzhdJNwL._AC_UL640_QL65_ML3_t.png", RatingRate = 4.6, RatingCount = 400, StockQuantity = 100 },
                new Product { Id = 6, Title = "Solid Gold Petite Micropave", Price = 168m, Description = "Satisfaction Guaranteed. Return or exchange any order within 30 days. Designed and sold by Hafeez Center in the United States.", Category = "jewelery", Image = "https://fakestoreapi.com/img/61sbMiUnoGL._AC_UL640_QL65_ML3_t.png", RatingRate = 3.9, RatingCount = 70, StockQuantity = 100 },
                new Product { Id = 7, Title = "White Gold Plated Princess", Price = 9.99m, Description = "Classic Created Wedding Engagement Solitaire Diamond Promise Ring for Her. Gifts to spoil your love more for Engagement, Wedding, Anniversary, Valentine's Day.", Category = "jewelery", Image = "https://fakestoreapi.com/img/71YAIFU48IL._AC_UL640_QL65_ML3_t.png", RatingRate = 3.0, RatingCount = 400, StockQuantity = 100 },
                new Product { Id = 8, Title = "Pierced Owl Rose Gold Plated Stainless Steel Double", Price = 10.99m, Description = "Rose Gold Plated Double Flared Tunnel Plug Earrings. Made of 316L Stainless Steel", Category = "jewelery", Image = "https://fakestoreapi.com/img/51UDEzMJVpL._AC_UL640_QL65_ML3_t.png", RatingRate = 1.9, RatingCount = 100, StockQuantity = 100 },
                new Product { Id = 9, Title = "WD 2TB Elements Portable External Hard Drive - USB 3.0", Price = 64m, Description = "USB 3.0 and USB 2.0 Compatibility Fast data transfers Improve PC Performance High Capacity; Compatibility Formatted NTFS for Windows 10, Windows 8.1, Windows 7.", Category = "electronics", Image = "https://fakestoreapi.com/img/61IBBVJvSDL._AC_SY879_t.png", RatingRate = 3.3, RatingCount = 203, StockQuantity = 100 },
                new Product { Id = 10, Title = "SanDisk SSD PLUS 1TB Internal SSD - SATA III 6 Gb/s", Price = 109m, Description = "Easy upgrade for faster boot up, shutdown, application load and response. Boosts burst write performance, making it ideal for typical PC workloads.", Category = "electronics", Image = "https://fakestoreapi.com/img/61U7T1koQqL._AC_SX679_t.png", RatingRate = 2.9, RatingCount = 470, StockQuantity = 100 },
                new Product { Id = 11, Title = "Silicon Power 256GB SSD 3D NAND A55 SLC Cache Performance Boost SATA III 2.5", Price = 109m, Description = "3D NAND flash are applied to deliver high transfer speeds Remarkable transfer speeds that enable faster bootup and improved overall system performance.", Category = "electronics", Image = "https://fakestoreapi.com/img/71kWymZ+c+L._AC_SX679_t.png", RatingRate = 4.8, RatingCount = 319, StockQuantity = 100 },
                new Product { Id = 12, Title = "WD 4TB Gaming Drive Works with Playstation 4 Portable External Hard Drive", Price = 114m, Description = "Expand your PS4 gaming experience, Play anywhere Fast and easy, setup Sleek design with high capacity, 3-year manufacturer's limited warranty.", Category = "electronics", Image = "https://fakestoreapi.com/img/61mtL65D4cL._AC_SX679_t.png", RatingRate = 4.8, RatingCount = 400, StockQuantity = 100 },
                new Product { Id = 13, Title = "Acer SB220Q bi 21.5 inches Full HD (1920 x 1080) IPS Ultra-Thin", Price = 599m, Description = "21.5 inches Full HD (1920 x 1080) widescreen IPS display And Radeon free Sync technology. No compatibility for VESA Mount Refresh Rate: 75Hz.", Category = "electronics", Image = "https://fakestoreapi.com/img/81QpkIctqPL._AC_SX679_t.png", RatingRate = 2.9, RatingCount = 250, StockQuantity = 100 },
                new Product { Id = 14, Title = "Samsung 49-Inch CHG90 144Hz Curved Gaming Monitor (LC49HG90DMNXZA) - Super Ultrawide Screen QLED", Price = 999.99m, Description = "49 INCH SUPER ULTRAWIDE 32:9 CURVED GAMING MONITOR with dual 27 inch screen side by side QUANTUM DOT (QLED) TECHNOLOGY, HDR support and factory calibration.", Category = "electronics", Image = "https://fakestoreapi.com/img/81Zt42ioCgL._AC_SX679_t.png", RatingRate = 2.2, RatingCount = 140, StockQuantity = 100 },
                new Product { Id = 15, Title = "BIYLACLESEN Women's 3-in-1 Snowboard Jacket Winter Coats", Price = 56.99m, Description = "Note: The Jackets is US standard size, Please choose size as your usual wear Material: 100% Polyester; Detachable Liner Fabric: Warm Fleece.", Category = "women's clothing", Image = "https://fakestoreapi.com/img/51Y5NI-I5jL._AC_UX679_t.png", RatingRate = 2.6, RatingCount = 235, StockQuantity = 100 },
                new Product { Id = 16, Title = "Lock and Love Women's Removable Hooded Faux Leather Moto Biker Jacket", Price = 29.95m, Description = "100% POLYURETHANE(shell) 100% POLYESTER(lining) 75% POLYESTER 25% COTTON (SWEATER), Faux leather material for style and comfort.", Category = "women's clothing", Image = "https://fakestoreapi.com/img/81XH0e8fefL._AC_UY879_t.png", RatingRate = 2.9, RatingCount = 340, StockQuantity = 100 },
                new Product { Id = 17, Title = "Rain Jacket Women Windbreaker Striped Climbing Raincoats", Price = 39.99m, Description = "Lightweight perfet for trip or casual wear---Long sleeve with hooded, adjustable drawstring waist design. Button and zipper front closure raincoat.", Category = "women's clothing", Image = "https://fakestoreapi.com/img/71HblAHs5xL._AC_UY879_-2t.png", RatingRate = 3.8, RatingCount = 679, StockQuantity = 100 },
                new Product { Id = 18, Title = "MBJ Women's Solid Short Sleeve Boat Neck V", Price = 9.85m, Description = "95% RAYON 5% SPANDEX, Made in USA or Imported, Do Not Bleach, Lightweight fabric with great stretch for comfort.", Category = "women's clothing", Image = "https://fakestoreapi.com/img/71z3kpMAYsL._AC_UY879_t.png", RatingRate = 4.7, RatingCount = 130, StockQuantity = 100 },
                new Product { Id = 19, Title = "Opna Women's Short Sleeve Moisture", Price = 7.95m, Description = "100% Polyester, Machine wash, 100% cationic polyester interlock, Machine Wash & Pre Shrunk for a Great Fit, Lightweight, roomy and highly breathable.", Category = "women's clothing", Image = "https://fakestoreapi.com/img/51eg55uWmdL._AC_UX679_t.png", RatingRate = 4.5, RatingCount = 146, StockQuantity = 100 },
                new Product { Id = 20, Title = "DANVOUY Womens T Shirt Casual Cotton Short", Price = 12.99m, Description = "95% Cotton, 5% Spandex, Features: Casual, Short Sleeve, Letter Print, V-Neck, Fashion Tees, The fabric is soft and has some stretch.", Category = "women's clothing", Image = "https://fakestoreapi.com/img/61pHAEJ4NML._AC_UX679_t.png", RatingRate = 3.6, RatingCount = 145, StockQuantity = 100 }
            );
        }
    }
}
