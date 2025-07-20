 # E-Commerce Frontend

 This is the frontend for an E-Commerce web application built with React and Redux. It provides a modern shopping experience, including product browsing, cart management, checkout, and user authentication.

 ## Features

 - **Product Listing**: Browse products with details and images.
 - **Cart Management**: Add, remove, and update quantities of products in the cart.
 - **Checkout Process**: Enter billing details and place orders.
 - **User Authentication**: Register, login, and manage orders.
 - **Order History**: View past orders.
 - **Responsive Design**: Works well on desktop and mobile devices.

 ## Project Structure

 ```
 public/
   ├─ favicon.ico
   ├─ index.html
   ├─ robots.txt
   └─ assets/
       └─ main.png.jpg
 src/
   ├─ api.js                # API calls
   ├─ index.js              # App entry point
   ├─ components/           # Reusable UI components
   │    ├─ Footer.jsx
   │    ├─ Navbar.jsx
   │    ├─ Products.jsx
   │    ├─ ScrollToTop.jsx
   │    └─ ...
   ├─ pages/                # Main pages
   │    ├─ AboutPage.jsx
   │    ├─ Cart.jsx
   │    ├─ Checkout.jsx
   │    ├─ ContactPage.jsx
   │    ├─ Home.jsx
   │    ├─ Login.jsx
   │    ├─ Orders.jsx
   │    ├─ Product.jsx
   │    ├─ Products.jsx
   │    ├─ Register.jsx
   │    └─ ...
   └─ redux/                # Redux store, actions, reducers
       ├─ store.js
       ├─ action/
       │    └─ index.js
       └─ reducer/
            ├─ handleAuth.js
            ├─ handleCart.js
            └─ index.js
 ```

 ## Getting Started

 ### Prerequisites
 - Node.js (v16 or above recommended)
 - npm (comes with Node.js)

 ### Installation
 1. **Clone the repository**
    ```pwsh
    git clone <your-repo-url>
    cd Frontend
    ```
 2. **Install dependencies**
    ```pwsh
    npm install
    ```
 3. **Start the development server**
    ```pwsh
    npm start
    ```
    The app will run at `http://localhost:3000` by default.

 ## Usage
 - Browse products on the home page.
 - Add products to your cart.
 - Proceed to checkout and fill in billing details.
 - Register or login to place orders and view order history.

 ## API Integration
 - All API calls are managed via `src/api.js`.
 - The checkout process sends order details to `/checkout/generate-invoice`.
 - Update the API base URL in `api.js` as needed for your backend.

 ## Customization
 - **Styling**: Modify CSS or use your own styles.
 - **Components**: Add or update components in `src/components`.
 - **Pages**: Add new pages in `src/pages`.
 - **Redux**: Update actions and reducers in `src/redux`.

 ## Folder Details
 - `public/`: Static assets and HTML template.
 - `src/components/`: Navbar, Footer, Product cards, etc.
 - `src/pages/`: Main app pages (Home, Cart, Checkout, etc.).
 - `src/redux/`: State management (store, actions, reducers).
 - `src/api.js`: API configuration and calls.

 ## Contributing
 1. Fork the repository.
 2. Create your feature branch (`git checkout -b feature/YourFeature`).
 3. Commit your changes (`git commit -m 'Add some feature'`).
 4. Push to the branch (`git push origin feature/YourFeature`).
 5. Open a pull request.

 
 ## Acknowledgements
 - [React](https://reactjs.org/)
 - [Redux](https://redux.js.org/)
 - [Bootstrap](https://getbootstrap.com/) (for UI styling)

 ---





