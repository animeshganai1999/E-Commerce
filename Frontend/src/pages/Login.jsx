import React, { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useDispatch } from 'react-redux';
import axios from "axios"; // Make sure you install axios: npm install axios
import { Footer, Navbar } from "../components";
import { setAuth } from "../redux/action";

const Login = () => {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const navigate = useNavigate();  // Initialize useNavigate hook

  const dispatch = useDispatch();

  const handleLogin = async (e) => {
    e.preventDefault();
    
    try {
      const response = await axios.post('https://localhost:7244/api/auth/login', {
        Email : email,
        Password : password
      }, {
        withCredentials: true
      });

      const { AccessToken, UserId } = response.data;
      // console.log('Access Token:', AccessToken);
      alert('Login successful!');

      // Optionally, you can save accessToken in memory or context
      dispatch(setAuth(AccessToken, UserId)); // Save accessToken and UserId in Redux store

      localStorage.setItem('accessToken', AccessToken);
      localStorage.setItem('userId', UserId); // Save UserId in local storage
      navigate('/');  // Redirect to home page

    } catch (error) {
      console.error('Login failed', error);
      alert('Login failed. Please check your credentials.');
      setEmail("");
      setPassword("");
    }
  };

  return (
    <>
      <Navbar />
      <div className="container my-3 py-3">
        <h1 className="text-center">Login</h1>
        <hr />
        <div className="row my-4 h-100">
          <div className="col-md-4 col-lg-4 col-sm-8 mx-auto">
            <form onSubmit={handleLogin}>
              <div className="my-3">
                <label htmlFor="email">Email address</label>
                <input
                  type="email"
                  className="form-control"
                  id="email"
                  placeholder="name@example.com"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  required
                />
              </div>
              <div className="my-3">
                <label htmlFor="password">Password</label>
                <input
                  type="password"
                  className="form-control"
                  id="password"
                  placeholder="Password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  required
                />
              </div>
              <div className="my-3">
                <p>
                  New Here?{" "}
                  <Link
                    to="/register"
                    className="text-decoration-underline text-info"
                  >
                    Register
                  </Link>
                </p>
              </div>
              <div className="text-center">
                <button className="my-2 mx-auto btn btn-dark" type="submit">
                  Login
                </button>
              </div>
            </form>
          </div>
        </div>
      </div>
      <Footer />
    </>
  );
};

export default Login;
