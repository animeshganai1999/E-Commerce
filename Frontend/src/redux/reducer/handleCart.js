const handleCart = (state = [], action) => {
  const product = action.payload;
  let updatedCart;

  switch (action.type) {
    case "SETCART":
      // Set the cart directly from DB (payload)
      updatedCart = action.payload;
      // localStorage.setItem("cart", JSON.stringify(updatedCart)); // Sync with localStorage
      return updatedCart;

    case "ADDITEM":
      // Check if product already in cart
      const exist = state.find((x) => x.id === product.id);
      if (exist) {
        // Increase the quantity
        updatedCart = state.map((x) =>
          x.id === product.id ? { ...x, Quantity: x.Quantity + 1 } : x
        );
      } else {
        updatedCart = [...state, { ...product, Quantity: 1 }];
      }
      // Update localStorage
      // localStorage.setItem("cart", JSON.stringify(updatedCart));
      return updatedCart;

    case "DELITEM":
      const exist2 = state.find((x) => x.id === product.id);
      if (exist2.Quantity === 1) {
        updatedCart = state.filter((x) => x.id !== exist2.id);
      } else {
        updatedCart = state.map((x) =>
          x.id === product.id ? { ...x, Quantity: x.Quantity - 1 } : x
        );
      }
      // Update localStorage
      // localStorage.setItem("cart", JSON.stringify(updatedCart));
      return updatedCart;
    case "CLEARCART":
      updatedCart = [];
      // Clear localStorage
      // localStorage.removeItem("cart");
      return updatedCart;
    default:
      return state;
  }
};

export default handleCart;
