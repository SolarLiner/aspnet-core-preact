import {h} from 'preact';
import {Link} from 'preact-router/match';
import style from './style.css';
import {useState, useEffect} from "preact/hooks";

const Header = () => {
    const [users, setUsers] = useState([]);
    useEffect(() => {
        fetch("/users").then(r => r.json()).then(d => setUsers(d));
    }, []);
    return <header class={style.header}>
        <h1>Preact App</h1>
        <nav>
            <Link activeClassName={style.active} href="/">Home</Link>
            {users.map(u => <Link activeClassName={style.active} href={`/profile/${u}`}>{u}</Link>)}
        </nav>
    </header>
};

export default Header;
