import React from 'react';
import clsx from 'clsx';
import Link from '@docusaurus/Link';
import useDocusaurusContext from '@docusaurus/useDocusaurusContext';
import Layout from '@theme/Layout';
import HomepageFeatures from '@site/src/components/HomepageFeatures';
import styles from './index.module.css';
import Translate, {translate} from '@docusaurus/Translate'; 

let logo;
try {
  logo = require('@site/static/img/logo/ma_logo.png');
} catch (ex) {
  logo = require('@site/static/img/logo/logo_fallback.png');
}

function HomepageHeader() {
  const {siteConfig} = useDocusaurusContext();
  return (
    <header className={clsx('hero hero--primary', styles.heroBanner)}>
      <div className="container">
        <h1 className="hero__title">
          <div className={styles.logo}>
            <img src={logo.default} alt="Modular Avatar"/>
          </div>
        </h1>
        <p className="hero__subtitle">
            <Translate>Drag-and-Drop Avatar Assembly</Translate>
        </p>
        <div className={styles.buttons}>
          <Link
            className={`button button--secondary button--lg ${styles.button}`}
            to="/docs/intro">
            <Translate>Documentation</Translate>
          </Link>
          <Link
              className={`button button--secondary button--lg ${styles.button}`}
              to="https://github.com/bdunderscore/modular-avatar/releases/latest">
              <Translate>Download</Translate>
          </Link>
          <Link
            className={`button button--secondary button--lg ${styles.button}`}
            to="/docs/tutorials/">
            <Translate>Tutorials</Translate>
          </Link>
        </div>
      </div>
    </header>
  );
}

export default function Home(): JSX.Element {
  const {siteConfig} = useDocusaurusContext();
  return (
    <Layout
      title={translate({message: 'Modular Avatar'})}
      description={translate({message: "Drag-and-Drop Avatar Assembly"})}>
      <HomepageHeader />
      <main>
          <HomepageFeatures />
      </main>
    </Layout>
  );
}
