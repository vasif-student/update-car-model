using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentCar.Areas.Admin.Constants;
using RentCar.Areas.Admin.Models.ViewModel;
using RentCar.Areas.Admin.Utilis;
using RentCar.DAL;
using RentCar.Data;
using RentCar.Models.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RentCar.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = RoleConstants.Admin)]
    public class CarController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly AppDbContext _dbContext;

        public CarController(AppDbContext dbContext, UserManager<IdentityUser> userManager)
        {
            _dbContext = dbContext;
            _userManager = userManager;
        }

        //***** Index *****//
        public async Task<IActionResult> Index()
        {
            IdentityUser user = await _userManager.GetUserAsync(User);
            var carModels = await _dbContext.CarModels.Where(c => !c.IsDeleted && c.OwnerId == user.Id)
                .Include(c => c.Car)
                .Include(c => c.District).ThenInclude(d => d.City)
                .Include(c => c.CarImages.Where(i => i.IsMain && !i.IsDeleted)).ToListAsync();


            return View(carModels);
        }

        //***** Car Detail *****//
        public async Task<IActionResult> CarDetail(int id)
        {
            var car = await _dbContext.CarModels
                .Include(c => c.Car)
                .Include(c => c.District).ThenInclude(d => d.City)
                .Include(c => c.CarImages.Where(i => !i.IsDeleted))
                .FirstOrDefaultAsync(c => c.Id == id);

            return View(new CarDetailViewModel
            {
                CarModel = car
            });
        }

        //***** Create *****//
        public async Task<IActionResult> Create()
        {
            var cars = await _dbContext.Cars.ToListAsync();
            var carModels = await _dbContext.CarModels.ToListAsync();
            var cities = await _dbContext.Cities.ToListAsync();
            var districts = await _dbContext.Districts.ToListAsync();

            return View(new CarCreateViewModel { 
                Cars = cars,
                CarModels = carModels,
                Cities = cities,
                Districts = districts
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CarCreateViewModel model)
        {
            IdentityUser user = await _userManager.GetUserAsync(User);

            if (!ModelState.IsValid)
            {
                return View();
            }

            var car = await _dbContext.Cars.Where(c => c.BrandName == model.CarBrandName).FirstOrDefaultAsync();
            var district = await _dbContext.Districts.Where(c => c.Name == model.District).FirstOrDefaultAsync();

            CarModel carModel = new CarModel
            {
                ModelName = model.CarModelName,
                Description = model.Description,
                CurrentPrice = model.Price,
                ReleaseYear = model.ReleaseYear,
                Color = model.Color,
                EngineSize = model.EngineSize,
                HorsePower = model.HorsePower,
                Fuel = model.Fuel,
                MileAge = model.MileAge,
                Transmission = model.Transmission,
                OwnerId = user.Id,
                Car = car,
                District = district
            };

            List<CarImage> carImages = new List<CarImage>();
            foreach(var image in model.Files)
            {
                if (image == null)
                {
                    ModelState.AddModelError("Files", "Select an image");
                    return View();
                }
                if (!image.IsSupported())
                {
                    ModelState.AddModelError("Files", "File is unsupported");
                    return View();
                }
                if (image.IsGreaterThanGivenSize(1024))
                {
                    ModelState.AddModelError(nameof(CarCreateViewModel.Files),
                        "File size cannot be greater than 1 mb");
                    return View();
                }
                var imgName = FileUtil.CreatedFile(Path.Combine(FileConstants.ImagePath, "cars"), image);
                carImages.Add(new CarImage { ImageName = imgName });
            }

            carModel.CarImages = carImages;

            await _dbContext.CarModels.AddAsync(carModel);
            await _dbContext.SaveChangesAsync();

            return RedirectToAction("Index", "Car");
        }

        //***** Delete *****//
        public async Task<IActionResult> Delete(int id)
        {
            CarModel carModel = await _dbContext.CarModels
                .Include(c => c.Car)
                .Include(c => c.CarImages.Where(i => i.IsMain)).FirstOrDefaultAsync(c => c.Id == id);
            if (carModel == null)
            {
                return NotFound();
            }
            return View(carModel);
        }

        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCarModel(int id)
        {
            CarModel carModel = await _dbContext.CarModels.FirstOrDefaultAsync(c => c.Id == id);
            if (carModel == null)
            {
                return NotFound();
            }
            carModel.IsDeleted = true;
            await _dbContext.SaveChangesAsync();
            return RedirectToAction(nameof(Index), "Car");
        }

        //***** Update *****//
        public async Task<IActionResult> Update(int id)
        {
            var cars = await _dbContext.Cars.ToListAsync();
            var carModels = await _dbContext.CarModels.ToListAsync();
            var cities = await _dbContext.Cities.ToListAsync();
            var districts = await _dbContext.Districts.ToListAsync();

            var carModel = await _dbContext.CarModels
                .Include(c => c.Car)
                .Include(c => c.District).ThenInclude(d => d.City)
                .Include(c => c.CarImages.Where(i => !i.IsDeleted))
                .FirstOrDefaultAsync(c => c.Id == id);

            return View(new CarUpdateViewModel { 
                Cars = cars,
                CarModels = carModels,
                Cities = cities,
                Districts = districts,
                CarModel = carModel
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(int id, CarUpdateViewModel model)
        {
            CarModel updatedCarModel = await _dbContext.CarModels
                .Include(c => c.Car)
                .Include(c => c.District).ThenInclude(d => d.City)
                .Include(c => c.CarImages.Where(i => !i.IsDeleted))
                .FirstOrDefaultAsync(c => c.Id == id);

            var car = await _dbContext.Cars.Where(c => c.BrandName == model.CarBrandName).FirstOrDefaultAsync();
            var district = await _dbContext.Districts.Where(c => c.Name == model.District).FirstOrDefaultAsync();

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            bool isExist = await _dbContext.CarModels.AnyAsync(c => c.Id == id);

            if (!isExist)
            {
                return NotFound();
            }

            if (model.CarModelName != null)
            {
                updatedCarModel.ModelName = model.CarModelName;
            }

            if(model.CarBrandName != null)
            {
                updatedCarModel.Car = car;
            }

            if(model.District != null)
            {
                updatedCarModel.District = district;
            }

            if(model.Description != null)
            {
                updatedCarModel.Description = model.Description;
            }

            if(model.Color != null)
            {
                updatedCarModel.Color = model.Color;
            }

            if(model.EngineSize != 0)
            {
                updatedCarModel.EngineSize = model.EngineSize;
            }

            if(model.HorsePower != 0)
            {
                updatedCarModel.HorsePower = model.HorsePower;
            }

            if(model.Fuel != null)
            {
                updatedCarModel.Fuel = model.Fuel;
            }

            if(model.Transmission != null)
            {
                updatedCarModel.Transmission = model.Transmission;
            }

            if(model.Price != 0)
            {
                updatedCarModel.OldPrice = updatedCarModel.CurrentPrice;
                updatedCarModel.CurrentPrice = model.Price;
            }

            List<CarImage> carImages = new List<CarImage>();

            if(model.Files != null)
            {
                foreach (var image in model.Files)
                {
                    if (image == null)
                    {
                        ModelState.AddModelError("Files", "Select an image");
                        return View();
                    }
                    if (!image.IsSupported())
                    {
                        ModelState.AddModelError("Files", "File is unsupported");
                        return View();
                    }
                    if (image.IsGreaterThanGivenSize(1024))
                    {
                        ModelState.AddModelError(nameof(CarCreateViewModel.Files),
                            "File size cannot be greater than 1 mb");
                        return View();
                    }
                    var imgName = FileUtil.CreatedFile(Path.Combine(FileConstants.ImagePath, "cars"), image);
                    carImages.Add(new CarImage { ImageName = imgName });
                }

                updatedCarModel.CarImages = carImages;
            }


            _dbContext.Update(updatedCarModel);
            await _dbContext.SaveChangesAsync();

            return RedirectToAction(nameof(Index), "Car");
        }


        //***** Add Photos *****//

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPhotos(PhotoCreateViewModel model)
        {
            var carModel = await _dbContext.CarModels.Where(c => c.Id == model.CarModelId).FirstOrDefaultAsync();

            List<CarImage> carImages = new List<CarImage>();

            foreach (var image in model.Files)
            {
                if (image == null)
                {
                    ModelState.AddModelError("Files", "Select an image");
                    return View();
                }
                if (!image.IsSupported())
                {
                    ModelState.AddModelError("Files", "File is unsupported");
                    return View();
                }
                if (image.IsGreaterThanGivenSize(1024))
                {
                    ModelState.AddModelError(nameof(PhotoCreateViewModel.Files),
                        "File size cannot be greater than 1 mb");
                    return View();
                }

                var imgName = FileUtil.CreatedFile(Path.Combine(FileConstants.ImagePath, "cars"), image);
                carImages.Add(new CarImage { ImageName = imgName });
            }

            carModel.CarImages = carImages;

            await _dbContext.SaveChangesAsync();

            return RedirectToAction("CarDetail", "Car", new { id = model.CarModelId });
        }

        //***** Delete Photo *****//
        public async Task<IActionResult> DeletePhoto(int id)
        {
            CarImage carImage = await _dbContext.CarImages.FindAsync(id);
            if (carImage == null)
            {
                return NotFound();
            }

            return View(carImage);
        }


        [HttpPost]
        [ActionName("DeletePhoto")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePhotoPost(int id)
        {
            CarImage carImage = await _dbContext.CarImages.FindAsync(id);
            if(carImage == null)
            {
                return NotFound();
            }

            carImage.IsDeleted = true;
            await _dbContext.SaveChangesAsync();

            //string path = Path.Combine(FileConstants.ImagePath, carImage.ImageName);
            //FileUtil.DeleteFile(path);

            //_dbContext.CarImages.Remove(carImage);
            //await _dbContext.SaveChangesAsync();


            return RedirectToAction("Index", "Car", new { id = id });
        }
    }
}
