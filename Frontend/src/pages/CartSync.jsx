import { useEffect, useRef } from 'react';
import { useSelector, useDispatch } from 'react-redux';
import axios from 'axios';
import { setCart } from "../redux/action"; 
import api from "../api"; 

const getCartDiff = (localCart, serverCart) => {
  localStorage.removeItem("cart");
  const add = [];
  const update = [];
  const del = [];

  const serverMap = new Map(serverCart.map(item => [item.id, item]));

  for (const item of localCart) {
    const serverItem = serverMap.get(item.id);
    if (!serverItem) {
      add.push(item);
    } else if (item.Quantity !== serverItem.Quantity) {
      update.push(item);
    }
    serverMap.delete(item.id);
  }

  for (const [id] of serverMap) {
    del.push(id);
  }
  return { add, update, delete: del };
};

// Update the cart in DB every 3 seconds if there are changes
export default function CartSync() {
  const cart = useSelector((state) => state.handleCart); // Contains the cart items (local state)
  const serverCartRef = useRef([]);
  const dispatch = useDispatch();
  
  const AccessToken = useSelector((state) => state.auth.accessToken); // Get Access Token from Redux store
  const currentUserId = useSelector((state) => state.auth.userId); // Get UserId from Redux store

  // const AccessToken = localStorage.getItem('accessToken');
  // const currentUserId = localStorage.getItem('userId'); // Get UserId from local storage
  // const currentUserId = "3F2504E0-4F89-11D3-9A0C-0305E82C3301"; // Get UserId from Redux store

  // console.log('Access Token:', AccessToken);

  // 🔹 Fetch cart from DB on first load
  useEffect(() => {
    console.log("Fetching cart from DB...");
    const fetchCart = async () => {
      try {
        const cartResponse = await api.get(`/cart/getItems?userId=${currentUserId}`, {
          withCredentials: true
        });
        const cartItems = cartResponse.data;
  
        const productsResponse = await axios.get('https://fakestoreapi.com/products/');
        const products = productsResponse.data;
  
        const fullCartItems = cartItems.map((item) => {
          const product = products.find(p => p.id === item.ProductId);
          return {
            ...item,
            ...product,
          };
        });
  
        serverCartRef.current = fullCartItems;
        dispatch(setCart(fullCartItems));
  
      } catch (error) {
        if (error.response && error.response.status === 404) {
          console.log("Cart is empty for user."); 
          serverCartRef.current = [];
          dispatch(setCart([])); // Set empty cart if no items found
        } else {
          console.error("Failed to fetch cart or product details:", error);
        }
      }
    };
  
    fetchCart();
  }, [AccessToken, dispatch, currentUserId]);
  
  
  
  // 🔹 Sync changes to DB every 3 seconds
  useEffect(() => {
    const interval = setInterval(async () => {
      const diff = getCartDiff(cart, serverCartRef.current);
  
      if (diff.add.length || diff.update.length || diff.delete.length) {
  
        const transformedDiff = {
          UserId: currentUserId,
          Added: diff.add.map(item => ({
            ProductId: item.id,
            Quantity: item.Quantity,
            UserId: currentUserId
          })),
          Updated: diff.update.map(item => ({
            ProductId: item.id,
            Quantity: item.Quantity,
            UserId: currentUserId
          })),
          Removed: diff.delete.map(ProductId => ({
            ProductId: ProductId,
            UserId: currentUserId
          }))
        };
        // console.log('Transformed Diff:', transformedDiff);
        try {
          await axios.post('https://localhost:7244/api/cart/update', transformedDiff);
          serverCartRef.current = [...cart];
        } catch (err) {
          console.error('Cart sync failed:', err);
        }
      }
    }, 3000);
  
    return () => clearInterval(interval);
  }, [cart, currentUserId]);
  

  return null; // This component doesn't render anything
}
