import React, { useState } from 'react';
import { createPortal } from 'react-dom';
import Modal from 'react-modal';

import clsx from 'clsx';
import Link from '@docusaurus/Link';
import useDocusaurusContext from '@docusaurus/useDocusaurusContext';
import Layout from '@theme/Layout';
import Translate, {translate} from '@docusaurus/Translate';

import styles from '@site/src/pages/index.module.css';

const install_help = require('@site/static/img/ma-install-help.png').default;

function ModalContent({closeModal}) {
    return <div className="card card--modal">
        <div className={"card__header"}>
            <h3><Translate>VPM installation</Translate></h3>
        </div>
        <div className={"card__body"}>
            <p>
                <Translate>
                    You should have seen a prompt to add Modular Avatar to VCC. If you didn't, upgrade your copy of the VRChat Creator Companion
                    and try again. Once you've added the repository, you can install Modular Avatar in your project by clicking
                    the button shown here:
                </Translate>
            </p>
            <img src={install_help} alt={translate({message: "Click the plus button to install"})}/>
        </div>
        <div className={"card__footer"}>
            <button className={"button button--secondary button--block"} onClick={closeModal}>
                <Translate>Close</Translate>
            </button>
        </div>
    </div>;
}

export default function InstallButton() {
    const [showModal, setShowModal] = useState(false);
    
    /*
                <!--{
                showModal && createPortal(
                    <ModalContent onClose={() => setShowModal(false)}/>,
                    document.body
                )
            }-->
     */
    
    return (
        <>
            <Link
                className={`button button--secondary button--lg ${styles.button}`}
                to="vcc://vpm/addRepo?url=https://vpm.nadena.dev/vpm.json"
                onClick={() => { setShowModal(true); return true; }}
            >
                <Translate>Download (using VCC)</Translate>
            </Link>
            { showModal && 
            <Modal isOpen={{showModal}} onRequestClose={() => setShowModal(false)}
                   contentLabel={"Example Modal"} className={"Modal"}>
                <ModalContent closeModal={() => setShowModal(false)}/>
            </Modal> }
        </>
    );
}