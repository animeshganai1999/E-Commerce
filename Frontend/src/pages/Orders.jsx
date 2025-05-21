import { Footer, Navbar } from "../components";

const Orders = () => {
    return (
        <>
        <Navbar />
        <div className="container my-5 py-5">
            <h1 className="text-center">Your Orders</h1>
            <div className="row">
            <div className="col-md-12 py-5 bg-light text-center">
                <h4 className="p-3 display-5">No Orders Found</h4>
            </div>
            </div>
        </div>
        <Footer />
        </>
    );
}

export default Orders;