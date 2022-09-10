import React from 'react';
import clsx from 'clsx';
import styles from './styles.module.css';
import Translate, {translate as t} from '@docusaurus/Translate';

type FeatureItem = {
  title: string;
  Svg: React.ComponentType<React.ComponentProps<'svg'>>;
  description: JSX.Element;
};

const FeatureList: FeatureItem[] = [
  {
    title: t({message: 'Drag and Drop assembly'}),
    Svg: require('@site/static/img/undraw_docusaurus_mountain.svg').default,
    description: (
      <Translate>
        Modular avatar merges components at build time. Never again will you forget to click 'install' or 'uninstall'
        when editing your avatar!  
      </Translate>
    ),
  },
  {
    title: t({message:'Organize your animators'}),
    Svg: require('@site/static/img/undraw_docusaurus_tree.svg').default,
    description: (
      <Translate>
        Split your avatar's FX animator into multiple sub-animators, and merge at runtime. Keep the animation edit
        dropdown tidy!
      </Translate>
    ),
  },
  {
    title: t({message: 'Perfect for prefabs'}),
    Svg: require('@site/static/img/undraw_docusaurus_react.svg').default,
    description: (
      <Translate>
        Embed modular avatar components in your prefabs to make installation a breeze!
      </Translate>
    ),
  },
];

function Feature({title, Svg, description}: FeatureItem) {
  return (
    <div className={clsx('col col--4')}>
      <div className="text--center">
        <Svg className={styles.featureSvg} role="img" />
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
