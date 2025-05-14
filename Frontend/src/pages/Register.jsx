import React from 'react'
import axios from "axios";
import { useDispatch } from 'react-redux';
import { Footer, Navbar } from "../components";
import { Link, useNavigate } from 'react-router-dom';
import { setAuth } from "../redux/action";

const Register = () => {
    const [email, setEmail] = React.useState("");
    const [password, setPassword] = React.useState("");
    const [name, setName] = React.useState("");
    const dispatch = useDispatch(); 

    const navigate = useNavigate();  // Initialize useNavigate hook

    const handleRegister = async (e) => {
        e.preventDefault();
        try {
            const response = await axios.post('https://localhost:7244/api/auth/register', {
                Email : email,
                Password : password,
                Name : name
            }, {
                withCredentials: true
            });
            const { AccessToken, UserId } = response.data;

            alert('Registration successful!');
            dispatch(setAuth(AccessToken, UserId)); 
            localStorage.setItem('accessToken', AccessToken);
            localStorage.setItem('userId', UserId); // Save UserId in local storage

            navigate('/');  // Redirect to home page
        } catch (error) {
            console.error('Registration failed', error);
            alert('Registration failed. Please check your details.');
        }
    }
    return (
        <>
            <Navbar />
            <div className="container my-3 py-3">
                <h1 className="text-center">Register</h1>
                <hr />
                <div className="row my-4 h-100">
                    <div className="col-md-4 col-lg-4 col-sm-8 mx-auto">
                        <form onSubmit={handleRegister}>
                            <div className="form my-3">
                                <label htmlFor="Name">Full Name</label>
                                <input
                                    type="text"
                                    className="form-control"
                                    id="Name"
                                    placeholder="Enter Your Name"
                                    value={name}
                                    onChange={(e) => setName(e.target.value)}
                                    required
                                />
                            </div>
                            <div className="form my-3">
                                <label htmlFor="Email">Email address</label>
                                <input
                                    type="email"
                                    className="form-control"
                                    id="Email"
                                    placeholder="name@example.com"
                                    value={email}
                                    onChange={(e) => setEmail(e.target.value)}
                                    required
                                />
                            </div>
                            <div className="form  my-3">
                                <label htmlFor="Password">Password</label>
                                <input
                                    type="password"
                                    className="form-control"
                                    id="Password"
                                    placeholder="Password"
                                    value={password}
                                    onChange={(e) => setPassword(e.target.value)}
                                    required
                                />
                            </div>
                            <div className="my-3">
                                <p>Already has an account? <Link to="/login" className="text-decoration-underline text-info">Login</Link> </p>
                            </div>
                            <div className="text-center">
                                <button className="my-2 mx-auto btn btn-dark" type="submit">
                                    Register
                                </button>
                            </div>
                        </form>
                    </div>
                </div>
            </div>
            <Footer />
        </>
    )
}

export default Register