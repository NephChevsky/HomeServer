import { useState } from 'react';
import { useNavigate } from 'react-router-dom';

function HomePage() {
    const navigate = useNavigate();
    const [message, setMessage] = useState('');
    const [messageType, setMessageType] = useState('');

    const handleLogout = () => {
        localStorage.removeItem('token');
        navigate('/login');
    };

    const getAuthHeaders = () => {
        const token = localStorage.getItem('token');
        return {
            'Content-Type': 'application/json',
            'Authorization': `Bearer ${token}`,
        };
    };

    const handleWakeUp = async () => {
        try {
            const API_URL = import.meta.env.VITE_API_URL;
            const response = await fetch(`${API_URL}/computer/wake`, {
                method: 'POST',
                headers: getAuthHeaders(),
            });
            if (response.status !== 200)
            {
                throw new Error(`Api responded with status code: ${response.status}`);
            }
            setMessage(`Wake up call successfull`);
            setMessageType('success');
        } catch (error) {
            console.error('Wake up error:', error);
            setMessage('Failed to send wake-up call');
            setMessageType('error');
        }
    };

    const handleRDP = async () => {
        try {
            const API_URL = import.meta.env.VITE_API_URL;
            const response = await fetch(`${API_URL}/computer/enable-rdp`, {
                method: 'POST',
                headers: getAuthHeaders(),
            });
            if (response.status !== 200)
            {
                throw new Error(`Api responded with status code: ${response.status}`);
            }
            setMessage(`RDP call successfull`);
            setMessageType('success');
        } catch (error) {
            console.error('RDP error:', error);
            setMessage('Failed to send RDP call');
            setMessageType('error');
        }
    };

    const messageStyle = {
        marginTop: '20px',
        padding: '10px',
        borderRadius: '4px',
        color: messageType === 'success' ? '#155724' : '#721c24',
        backgroundColor: messageType === 'success' ? '#d4edda' : '#f8d7da',
        border: `1px solid ${messageType === 'success' ? '#c3e6cb' : '#f5c6cb'}`,
        maxWidth: '400px',
    };

    return (
        <div style={{ display: 'flex', height: '100vh' }}>
            <nav
                style={{
                    width: '200px',
                    background: '#333',
                    color: '#fff',
                    display: 'flex',
                    flexDirection: 'column',
                    padding: '20px',
                }}
            >
                <h2>HomeServer</h2>
                <button
                    onClick={handleLogout}
                    style={{
                        marginTop: 'auto',
                        padding: '10px',
                        background: '#ff4d4f',
                        border: 'none',
                        color: 'white',
                        cursor: 'pointer',
                        borderRadius: '4px',
                    }}
                >
                    Logout
                </button>
            </nav>

            <main style={{ flex: 1, padding: '20px' }}>
                <h1>Welcome to the Homepage!</h1>

                <div style={{ marginTop: '20px', display: 'flex', gap: '10px' }}>
                    <button
                        onClick={handleWakeUp}
                        style={{
                            padding: '10px 20px',
                            background: '#1890ff',
                            color: 'white',
                            border: 'none',
                            borderRadius: '4px',
                            cursor: 'pointer',
                        }}
                    >
                        Wake up
                    </button>

                    <button
                        onClick={handleRDP}
                        style={{
                            padding: '10px 20px',
                            background: '#1890ff',
                            color: 'white',
                            border: 'none',
                            borderRadius: '4px',
                            cursor: 'pointer',
                        }}
                    >
                        RDP
                    </button>
                </div>

                {message && <div style={messageStyle}>{message}</div>}
            </main>
        </div>
    );
}

export default HomePage;
