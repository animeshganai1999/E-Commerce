const initialState = {
    accessToken: localStorage.getItem('accessToken') || null,
    userId: localStorage.getItem('userId') || null
  };
  
  const handleAuth = (state = initialState, action) => {
    switch (action.type) {
      case "SETAUTH":
        return {
          ...state,
          accessToken: action.payload.accessToken,
          userId: action.payload.userId,
        };
      default:
        return state;
    }
  };
  
  export default handleAuth;
  