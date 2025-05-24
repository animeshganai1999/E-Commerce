import React from "react";
import { Footer, Navbar } from "../components";
import axios from "axios";

const handleContact = async (e) => {
  e.preventDefault();
  const name = document.getElementById("Name").value;
  const email = document.getElementById("Email").value;
  const message = document.getElementById("Password").value;

  // Perform your contact form submission logic here
  try{
    const response = await axios.post('https://localhost:7244/api/email/send', {
      Name: name,
      Email: email,
      Message: message
    });
    // console.log("Response:", response.data);
    alert("Message sent successfully!");
  }catch(error){
    console.error("Error sending message:", error);
    alert("Failed to send message. Please try again later.");
  }

  // Optionally, you can reset the form fields after submission
  document.getElementById("Name").value = "";
  document.getElementById("Email").value = "";
  document.getElementById("Password").value = "";
};

const ContactPage = () => {
  return (
    <>
      <Navbar />
      <div className="container my-3 py-3">
        <h1 className="text-center">Contact Us</h1>
        <hr />
        <div className="row my-4 h-100">
          <div className="col-md-4 col-lg-4 col-sm-8 mx-auto">
            <form onSubmit={handleContact}>
              <div className="form my-3">
                <label htmlFor="Name">Name</label>
                <input
                  type="text"
                  className="form-control"
                  id="Name"
                  placeholder="Enter your name"
                  required
                />
              </div>
              <div className="form my-3">
                <label htmlFor="Email">Email</label>
                <input
                  type="email"
                  className="form-control"
                  id="Email"
                  placeholder="name@example.com"
                  required
                />
              </div>
              <div className="form  my-3">
                <label htmlFor="Password">Message</label>
                <textarea
                  rows={5}
                  className="form-control"
                  id="Password"
                  placeholder="Enter your message"
                  required
                />
              </div>
              <div className="text-center">
                <button
                  className="my-2 px-4 mx-auto btn btn-dark"
                  type="submit"
                >
                  Send
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

export default ContactPage;
