import React from 'react';
import clsx from 'clsx';
import styles from './styles.module.css';
import Translate, {translate as t} from '@docusaurus/Translate';

type FeatureItem = {
  title: string;
  image: string;
  description: JSX.Element;
};

const FeatureList: FeatureItem[] = [
  {
    title: t({message: 'Drag and Drop assembly'}),
    image: require('@site/static/img/irasutoya/prefab-drag.png').default,
    description: (
      <Translate>
        Modular avatar merges components at build time. Never again will you forget to click 'install' or 'uninstall'
        when editing your avatar!  
      </Translate>
    ),
  },
  {
    title: t({message:'Organize your animators'}),
    image: require('@site/static/img/irasutoya/tana_seiriseiton_yes.png').default,
    description: (
      <Translate>
        Split your avatar's FX animator into multiple sub-animators, and merge at runtime. Keep the animation edit
        dropdown tidy!
      </Translate>
    ),
  },
  {
    title: t({message: 'Perfect for prefabs'}),
    image: require('@site/static/img/irasutoya/pack-prefab.png').default,
    description: (
      <Translate>
        Embed modular avatar components in your prefabs to make installation a breeze!
      </Translate>
    ),
  },
];

function Feature({title, image, description}: FeatureItem) {
  return (
    <div className={clsx('col col--4')}>
      <div className="text--center">
        <div className={styles.featureImage}>
          <img src={image} role="img" />
        </div>
      </div>
      <div className="text--center padding-horiz--md">
        <h3>{title}</h3>
        <p>{description}</p>
      </div>
    </div>
  );
}

export default function HomepageFeatures(): JSX.Element {
  return (
    <section className={styles.features}>
      <div className="container">
        <div className="row">
          {FeatureList.map((props, idx) => (
            <Feature key={idx} {...props} />
          ))}
        </div>
      </div>
    </section>
  );
}
