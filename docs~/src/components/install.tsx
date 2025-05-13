import React, { useState } from 'react';
import { createPortal } from 'react-dom';
import Modal from 'react-modal';

import clsx from 'clsx';
import Link from '@docusaurus/Link';
import useDocusaurusContext from '@docusaurus/useDocusaurusContext';
import Layout from '@theme/Layout';
import Translate, {translate} from '@docusaurus/Translate';

import Tabs from '@theme/Tabs';
import TabItem from '@theme/TabItem';

import styles from '@site/src/pages/index.module.css';

const install_help_alcom = require('@site/static/img/ma-install-help-alcom.png').default;
const install_help_vcc = require('@site/static/img/ma-install-help-vcc.png').default;

function ModalContent({closeModal}) {
    return <div className="card card--modal">
        <div className={"card__header"}>
            <h3><Translate>VPM installation</Translate></h3>
        </div>
        <div className={"card__body"}>
            <p>
                <Translate>
                    You should have seen a prompt to add Modular Avatar to ALCOM or VCC. If you didn't, try installing or reinstalling ALCOM or VCC using the links below.
                    Once you've added the repository, you can install Modular Avatar in your project by clicking the button shown below.
                </Translate>
            </p>
            <p>
                <a href="docs/problems/install">
                    <Translate>
                        Something went wrong? Click here.
                    </Translate>
                </a>
            </p>

            <Tabs>
                <TabItem value="alcom" label="ALCOM" default>
                    <a href="https://vrc-get.anatawa12.com/en/alcom/"><Translate>Download ALCOM here</Translate></a>
                    <img src={install_help_alcom} alt={translate({message: "Click the plus button to install"})}/>
                </TabItem>
                <TabItem value="vcc" label="VRChat Creator Companion">
                    <a href="https://vrchat.com/home/download"><Translate>Download VRChat Creator Companion here</Translate></a>
                    <img src={install_help_vcc} alt={translate({message: "Click the plus button to install"})}/>
                </TabItem>
            </Tabs>
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
                <Translate>Download</Translate>
            </Link>
            { showModal && 
            <Modal isOpen={{showModal}} onRequestClose={() => setShowModal(false)}
                   contentLabel={"Example Modal"} className={"Modal"}>
                <ModalContent closeModal={() => setShowModal(false)}/>
            </Modal> }
        </>
    );
}