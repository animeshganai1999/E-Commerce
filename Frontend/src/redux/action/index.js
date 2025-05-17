// For Add Item to Cart
export const addCart = (product) =>{
    return {
        type:"ADDITEM",
        payload:product
    }
}

// For Delete Item to Cart
export const delCart = (product) =>{
    return {
        type:"DELITEM",
        payload:product
    }
}

// Clear Cart
export const clearCart = () => {
    return {
        type: "CLEARCART",
    }
}

// For Setting the Cart from DB
export const setCart = (cartItems) => {
    return {
        type: "SETCART",
        payload: cartItems
    }
}

// For Setting the Auth from DB
export const setAuth = (accessToken, userId) => {
    return {
        type: "SETAUTH",
        payload: { accessToken, userId }
    }
}