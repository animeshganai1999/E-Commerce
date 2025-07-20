# E-Commerce Project

A full-stack E-Commerce application with a React frontend and ASP.NET Core backend.

## Table of Contents

- [Overview](#overview)
- [Tech Stack](#tech-stack)
- [Project Structure](#project-structure)
- [Setup & Installation](#setup--installation)
- [Features](#features)
- [API Endpoints](#api-endpoints)
- [Contributing](#contributing)

---

## Overview

This project is a modern E-Commerce platform supporting user authentication, product browsing, cart management, checkout, and order history. The backend is built with ASP.NET Core, and the frontend uses React and Redux.

---

## Tech Stack

- **Frontend:** React, Redux, JavaScript
- **Backend:** ASP.NET Core (.NET 6+), Entity Framework Core
- **Database:** SQL Server (default, configurable)
- **Other:** JWT Authentication, AutoMapper

---

## Project Structure

```
E-Commerce/
│
├── Backend/
│   └── ECommerceBackend/
│       ├── ECommerceBackend.API/           # ASP.NET Core Web API
│       ├── ECommerceBackend.Application/   # Application logic, DTOs, Services
│       ├── ECommerceBackend.Domain/        # Domain models/entities
│       └── ECommerceBackend.Infrastructure/# Data access, Repositories
│
└── Frontend/
    ├── public/                             # Static assets
    └── src/
        ├── components/                     # Reusable React components
        ├── pages/                          # Page-level React components
        └── redux/                          # Redux store, actions, reducers
```

---

## Setup & Installation

### Backend

1. Navigate to `Backend/ECommerceBackend/ECommerceBackend.API`
2. Restore NuGet packages:
   ```
   dotnet restore
   ```
3. Update database (if using migrations):
   ```
   dotnet ef database update
   ```
4. Run the API:
   ```
   dotnet run
   ```
   The API will be available at `https://localhost:5001` (or as configured).

### Frontend

1. Navigate to `Frontend`
2. Install dependencies:
   ```
   npm install
   ```
3. Start the development server:
   ```
   npm start
   ```
   The app will be available at `http://localhost:3000`

---

## Features

- User registration & login (JWT-based)
- Product listing & details
- Cart management (add, remove, update items)
- Checkout & order placement
- Order history
- Email notifications (backend)
- Cart sync between frontend and backend

---

## API Endpoints

Some key endpoints (see backend controllers for details):

- `POST /api/auth/login` - User login
- `POST /api/auth/register` - User registration
- `GET /api/products` - List products
- `POST /api/cart` - Update cart
- `POST /api/checkout` - Place order
- `GET /api/orders` - Get user orders

---

## Contributing

1. Fork the repository
2. Create a new branch (`git checkout -b feature-name`)
3. Commit your changes
4. Push to your branch
5. Create a pull request

---

