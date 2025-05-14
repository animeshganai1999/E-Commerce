import handleAuth from './handleAuth';
import handleCart from './handleCart'
import { combineReducers } from "redux";
const rootReducers = combineReducers({
    handleCart,
    auth: handleAuth
})
export default rootReducers