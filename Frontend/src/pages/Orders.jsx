import { useEffect, useState } from "react";
import { Footer, Navbar } from "../components";
import { Link } from "react-router-dom";
import { useSelector } from 'react-redux';
import api from "../api"; 
import axios from "axios";

const Orders = () => {
    const currentUserId = useSelector((state) => state.auth.userId);
    const [orders, setOrders] = useState([]);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        const fetchOrders = async () => {
            try {
                const response = await api.get(`/ordereditems/get-invoice?userId=${currentUserId}`);
                setOrders(response.data);
                // console.log("Orders fetched:", response.data);
            } catch (error) {
                setOrders([]);
            } finally {
                setLoading(false);
            }
        };
        if (currentUserId) fetchOrders();
    }, [currentUserId]);

    const handleDownloadInvoice = async (invoiceUrl, idx) => {
        try {
            const response = await axios.get(invoiceUrl, {
                responseType: 'blob',
            });
            const url = window.URL.createObjectURL(new Blob([response.data]));
            const link = document.createElement('a');
            link.href = url;
            link.setAttribute('download', `invoice_${idx + 1}.pdf`);
            document.body.appendChild(link);
            link.click();
            link.remove();
            window.URL.revokeObjectURL(url);
        } catch (error) {
            alert("Failed to download invoice.");
        }
    };

    const EmptyOrder = () => (
        <div className="container">
            <div className="row">
                <div className="col-md-12 py-5 bg-light text-center">
                    <h4 className="p-3 display-5">You have not ordered anything</h4>
                    <Link to="/" className="btn btn-outline-dark mx-4">
                        <i className="fa fa-arrow-left"></i> Continue Shopping
                    </Link>
                </div>
            </div>
        </div>
    );

    const ShowOrders = () => (
    <section className="h-100 gradient-custom">
        <div className="container py-5">
            <div className="row d-flex justify-content-center my-4">
                <div className="col-md-8">
                    <div className="card mb-4">
                        <div className="card-header py-3">
                            <h5 className="mb-0">Order List</h5>
                        </div>
                        <div className="card-body">
                            {orders.length === 0 ? (
                                <p>No orders to display.</p>
                            ) : (
                                <div className="d-flex flex-column gap-4">
                                    {orders.map((order, idx) => (
                                        <div
                                            key={order.id || idx}
                                            className="border rounded p-3 shadow-sm bg-light"
                                        >
                                            <div className="d-flex flex-column flex-md-row justify-content-between align-items-md-center mb-2">
                                                <div>
                                                    <span className="fw-bold">Order #{idx + 1}</span>
                                                </div>
                                                <div>
                                                    <span className="text-muted small">
                                                        {order.InvoiceDate ? order.InvoiceDate.split('T')[0] : ''}
                                                    </span>
                                                </div>
                                            </div>
                                            <div className="d-flex flex-wrap justify-content-between align-items-center mb-2">
                                                <div>
                                                    <span className="fw-semibold">Number of Items: </span>
                                                    {order.NumberOfItems}
                                                </div>
                                                <div>
                                                    <span className="fw-semibold">Total: </span>
                                                    <span className="fs-5 fw-bold text-success">₹{order.TotalAmount}</span>
                                                </div>
                                            </div>
                                            <div className="d-flex justify-content-end">
                                                <button
                                                    type="button"
                                                    className="btn btn-sm btn-outline-primary"
                                                    style={{ whiteSpace: "nowrap" }}
                                                    onClick={() => handleDownloadInvoice(order.InvoiceLink, idx)}
                                                >
                                                    Download Invoice
                                                </button>
                                            </div>
                                        </div>
                                    ))}
                                </div>
                            )}
                        </div>
                    </div>
                </div>
            </div>
        </div>
    </section>
);

    return (
        <>
            <Navbar />
            <div className="container my-3 py-3">
                <h1 className="text-center">Your Orders</h1>
                <hr />
                {loading ? (
                    <div className="text-center">Loading...</div>
                ) : orders.length === 0 ? (
                    <EmptyOrder />
                ) : (
                    <ShowOrders />
                )}
            </div>
            <Footer />
        </>
    );
};

export default Orders;