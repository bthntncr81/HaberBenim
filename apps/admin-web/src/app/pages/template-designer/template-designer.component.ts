import { Component, OnInit, OnDestroy, inject, signal, computed, ElementRef, ViewChild, AfterViewInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import Konva from 'konva';
import { 
  TemplateApiService, 
  TemplateDto, 
  TemplateSpecDto, 
  VisualSpec, 
  LayerSpec, 
  TextSpec,
  TemplatePreviewResponse,
  ContentItemBasic,
  GradientSpec
} from '../../services/template-api.service';

@Component({
  selector: 'app-template-designer',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './template-designer.component.html',
  styleUrls: ['./template-designer.component.scss']
})
export class TemplateDesignerComponent implements OnInit, AfterViewInit, OnDestroy {
  private api = inject(TemplateApiService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  @ViewChild('canvasContainer') canvasContainer!: ElementRef<HTMLDivElement>;

  // Template data
  templateId = signal('');
  template = signal<TemplateDto | null>(null);
  loading = signal(true);
  error = signal<string | null>(null);
  saving = signal(false);
  saveSuccess = signal(false);

  // Canvas
  private stage: Konva.Stage | null = null;
  private layer: Konva.Layer | null = null;
  private transformer: Konva.Transformer | null = null;

  // Visual spec
  visualSpec = signal<VisualSpec>({
    canvas: { w: 1080, h: 1080, bg: '#0b0f1a' },
    layers: []
  });

  // Text spec
  textSpec = signal<TextSpec>({
    instagramCaption: '{title}\n\n{summary}\n\nKaynak: {sourceName}\n{hashtags}',
    xText: '{title}\n\n{url}',
    tiktokHook: 'SON DAKİKA: {title}',
    youtubeTitle: '{title}',
    youtubeDescription: '{summary}\n\nKaynak: {sourceName}\n{url}'
  });

  // Selected layer
  selectedLayerId = signal<string | null>(null);
  selectedLayer = computed(() => {
    const id = this.selectedLayerId();
    if (!id) return null;
    return this.visualSpec().layers.find(l => l.id === id) || null;
  });

  // Reversed layers for display (top of list = front = last in array)
  reversedLayers = computed(() => {
    return [...this.visualSpec().layers].reverse();
  });

  // Preview dialog
  showPreviewDialog = signal(false);
  previewPlatform = signal<string>('Instagram');

  // Variables
  variables = [
    { key: 'title', label: 'Başlık' },
    { key: 'summary', label: 'Özet' },
    { key: 'category', label: 'Kategori' },
    { key: 'sourceName', label: 'Kaynak' },
    { key: 'url', label: 'URL' },
    { key: 'publishedAt', label: 'Tarih' },
    { key: 'hashtags', label: 'Hashtagler' }
  ];

  // Preview
  previewLoading = signal(false);
  previewUrl = signal<string | null>(null);
  previewError = signal<string | null>(null);
  recentContent = signal<ContentItemBasic[]>([]);
  selectedContentId = signal('');

  // Logo upload
  uploadedAssets = signal<{key: string; url: string}[]>([]);
  uploading = signal(false);

  // Canvas scale for display
  canvasScale = 0.5;

  ngOnInit() {
    this.route.params.subscribe(params => {
      this.templateId.set(params['id']);
      this.loadTemplate();
      this.loadRecentContent();
      this.loadAssets();
    });
  }

  ngAfterViewInit() {
    // Canvas will be initialized after data is loaded
  }

  ngOnDestroy() {
    if (this.stage) {
      this.stage.destroy();
    }
  }

  loadTemplate() {
    this.loading.set(true);
    
    this.api.get(this.templateId()).subscribe({
      next: (template) => {
        this.template.set(template);
        this.loadSpec();
      },
      error: (err) => {
        this.error.set(err.error?.error || 'Failed to load template');
        this.loading.set(false);
      }
    });
  }

  loadSpec() {
    this.api.getSpec(this.templateId()).subscribe({
      next: (spec) => {
        if (spec.visualSpecJson) {
          try {
            const vs = JSON.parse(spec.visualSpecJson);
            this.visualSpec.set(vs);
          } catch (e) {
            console.error('Failed to parse visual spec', e);
          }
        }
        if (spec.textSpecJson) {
          try {
            const ts = JSON.parse(spec.textSpecJson);
            this.textSpec.set({ ...this.textSpec(), ...ts });
          } catch (e) {
            console.error('Failed to parse text spec', e);
          }
        }
        this.loading.set(false);
        // Initialize canvas after data is loaded and view updated
        setTimeout(() => {
          this.initCanvas();
        }, 100);
      },
      error: (err) => {
        this.error.set(err.error?.error || 'Failed to load spec');
        this.loading.set(false);
      }
    });
  }

  loadRecentContent() {
    console.log('Loading recent content...');
    this.api.getRecentContent().subscribe({
      next: (res) => {
        console.log('Recent content response:', res);
        const items = res.items.map((item: any) => ({
          id: item.id,
          title: item.webTitle || item.title || 'Untitled',
          sourceName: item.sourceName || 'Unknown',
          publishedAtUtc: item.publishedAtUtc
        }));
        console.log('Mapped items:', items);
        this.recentContent.set(items);
        if (items.length > 0) {
          this.selectedContentId.set(items[0].id);
          console.log('Selected content ID:', items[0].id);
        }
      },
      error: (err) => {
        console.error('Failed to load recent content:', err);
      }
    });
  }

  loadAssets() {
    this.api.listAssets().subscribe({
      next: (res) => {
        const assets = res.items.map(a => ({ key: a.key, url: a.storagePath }));
        this.uploadedAssets.set(assets);
      },
      error: (err) => {
        console.error('Failed to load assets:', err);
      }
    });
  }

  onLogoFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    if (!input.files || input.files.length === 0) return;

    const file = input.files[0];
    const key = `logo_${Date.now()}`;

    this.uploading.set(true);
    
    this.api.uploadAsset(key, file).subscribe({
      next: (asset) => {
        this.uploading.set(false);
        // Add to list
        this.uploadedAssets.update(list => [...list, { key: asset.key, url: asset.storagePath }]);
        // Select the new asset
        this.updateLayerProperty('assetKey', asset.key);
        console.log('Asset uploaded:', asset);
      },
      error: (err) => {
        this.uploading.set(false);
        console.error('Failed to upload asset:', err);
        this.error.set(err.error?.error || 'Logo yükleme başarısız');
      }
    });

    // Reset input
    input.value = '';
  }

  initCanvas() {
    if (!this.canvasContainer?.nativeElement) {
      console.error('Canvas container not found');
      return;
    }

    const container = this.canvasContainer.nativeElement;
    const spec = this.visualSpec();
    
    console.log('Initializing canvas with spec:', spec);
    
    // Destroy existing stage if any
    if (this.stage) {
      this.stage.destroy();
    }
    
    this.stage = new Konva.Stage({
      container: container,
      width: spec.canvas.w * this.canvasScale,
      height: spec.canvas.h * this.canvasScale,
      scaleX: this.canvasScale,
      scaleY: this.canvasScale
    });

    this.layer = new Konva.Layer();
    this.stage.add(this.layer);

    // Add transformer with all anchors
    this.transformer = new Konva.Transformer({
      nodes: [],
      enabledAnchors: [
        'top-left', 'top-center', 'top-right',
        'middle-left', 'middle-right',
        'bottom-left', 'bottom-center', 'bottom-right'
      ],
      rotateEnabled: false,
      boundBoxFunc: (oldBox, newBox) => {
        // Limit resize to positive dimensions
        if (newBox.width < 10) newBox.width = 10;
        if (newBox.height < 10) newBox.height = 10;
        return newBox;
      }
    });
    this.layer.add(this.transformer);

    // Click on stage to deselect
    this.stage.on('click', (e) => {
      if (e.target === this.stage) {
        this.selectLayer(null);
      }
    });

    this.renderCanvas();
  }

  renderCanvas() {
    if (!this.layer || !this.stage) return;

    const spec = this.visualSpec();
    const selectedId = this.selectedLayerId();
    
    // Update stage size
    this.stage.width(spec.canvas.w * this.canvasScale);
    this.stage.height(spec.canvas.h * this.canvasScale);

    // Clear existing shapes (except transformer)
    const children = [...this.layer.children];
    children.forEach(child => {
      if (!(child instanceof Konva.Transformer)) {
        child.destroy();
      }
    });

    // Draw background
    const bg = new Konva.Rect({
      x: 0,
      y: 0,
      width: spec.canvas.w,
      height: spec.canvas.h,
      fill: spec.canvas.bg
    });
    this.layer.add(bg);
    bg.moveToBottom();

    // Draw layers
    spec.layers.forEach(layerSpec => {
      const shape = this.createShape(layerSpec);
      if (shape) {
        this.layer!.add(shape);
        this.setupDragResize(shape, layerSpec);
      }
    });

    // Re-add transformer on top and reattach to selected node
    this.transformer?.moveToTop();
    
    // Reattach transformer to selected node
    if (selectedId && this.transformer) {
      const node = this.layer.findOne('#' + selectedId);
      if (node) {
        this.transformer.nodes([node as Konva.Node]);
      } else {
        this.transformer.nodes([]);
      }
    }
    
    this.layer.batchDraw();
  }

  createShape(spec: LayerSpec): Konva.Shape | Konva.Group | null {
    const opacity = spec.opacity ?? 1;

    switch (spec.type) {
      case 'rect':
        const rectConfig: any = {
          id: spec.id,
          x: spec.x,
          y: spec.y,
          width: spec.w,
          height: spec.h,
          cornerRadius: spec.radius || 0,
          opacity: opacity,
          draggable: true
        };

        // Apply gradient or solid fill
        if (spec.fillGradient && spec.fillGradient.colors?.length >= 2) {
          const angle = (spec.fillGradient.angle || 0) * Math.PI / 180;
          const cos = Math.cos(angle);
          const sin = Math.sin(angle);
          const halfW = spec.w / 2;
          const halfH = spec.h / 2;
          
          rectConfig.fillLinearGradientStartPoint = {
            x: halfW - cos * halfW,
            y: halfH - sin * halfH
          };
          rectConfig.fillLinearGradientEndPoint = {
            x: halfW + cos * halfW,
            y: halfH + sin * halfH
          };
          rectConfig.fillLinearGradientColorStops = [
            0, spec.fillGradient.colors[0],
            1, spec.fillGradient.colors[1]
          ];
        } else {
          rectConfig.fill = spec.fill || '#333333';
        }

        return new Konva.Rect(rectConfig);

      case 'text':
        const text = new Konva.Text({
          id: spec.id,
          x: spec.x,
          y: spec.y,
          width: spec.w,
          height: spec.h,
          text: spec.bind || 'Text',
          fontSize: spec.fontSize || 32,
          fontStyle: (spec.fontWeight || 400) >= 700 ? 'bold' : 'normal',
          fill: spec.color || '#ffffff',
          align: spec.align || 'left',
          opacity: opacity,
          draggable: true
        });
        return text;

      case 'image':
        const imgRect = new Konva.Rect({
          id: spec.id,
          x: spec.x,
          y: spec.y,
          width: spec.w,
          height: spec.h,
          fill: '#2a2a3e',
          stroke: '#3a3a4e',
          strokeWidth: 2,
          cornerRadius: spec.radius || 0,
          opacity: opacity,
          draggable: true
        });
        return imgRect;

      case 'asset':
        const assetRect = new Konva.Rect({
          id: spec.id,
          x: spec.x,
          y: spec.y,
          width: spec.w,
          height: spec.h,
          fill: '#1a4a2e',
          stroke: '#22c55e',
          strokeWidth: 2,
          opacity: opacity,
          draggable: true
        });
        return assetRect;

      default:
        return null;
    }
  }

  setupDragResize(shape: Konva.Shape | Konva.Group, spec: LayerSpec) {
    shape.on('click tap', () => {
      this.selectLayer(spec.id);
    });

    // Update position during drag (not just at end)
    shape.on('dragmove', () => {
      // Keep transformer attached during drag
      this.transformer?.forceUpdate();
    });

    shape.on('dragend', () => {
      const newX = Math.round(shape.x());
      const newY = Math.round(shape.y());
      this.updateLayerPositionSilent(spec.id, newX, newY);
    });

    // Update during transform for visual feedback
    shape.on('transform', () => {
      // Keep transformer attached
      this.transformer?.forceUpdate();
    });

    shape.on('transformend', () => {
      const scaleX = shape.scaleX();
      const scaleY = shape.scaleY();
      
      // Calculate new dimensions
      const newWidth = Math.round(shape.width() * scaleX);
      const newHeight = Math.round(shape.height() * scaleY);
      const newX = Math.round(shape.x());
      const newY = Math.round(shape.y());
      
      // Reset scale and apply new dimensions to shape directly
      shape.scaleX(1);
      shape.scaleY(1);
      shape.width(newWidth);
      shape.height(newHeight);
      
      // Update the spec silently (don't trigger re-render)
      this.updateLayerSizeSilent(spec.id, newX, newY, newWidth, newHeight);
      
      // Update transformer
      this.transformer?.forceUpdate();
      this.layer?.batchDraw();
    });
  }

  selectLayer(id: string | null) {
    this.selectedLayerId.set(id);
    
    if (this.transformer && this.layer) {
      if (id) {
        const node = this.layer.findOne('#' + id);
        if (node) {
          this.transformer.nodes([node as Konva.Node]);
        }
      } else {
        this.transformer.nodes([]);
      }
      this.layer.draw();
    }
  }

  updateLayerPosition(id: string, x: number, y: number) {
    this.visualSpec.update(vs => ({
      ...vs,
      layers: vs.layers.map(l => 
        l.id === id ? { ...l, x: Math.round(x), y: Math.round(y) } : l
      )
    }));
  }

  // Silent updates - don't trigger re-render
  updateLayerPositionSilent(id: string, x: number, y: number) {
    const vs = this.visualSpec();
    const updatedLayers = vs.layers.map(l => 
      l.id === id ? { ...l, x, y } : l
    );
    // Use set instead of update to avoid triggering effects
    this.visualSpec.set({ ...vs, layers: updatedLayers });
  }

  updateLayerSizeSilent(id: string, x: number, y: number, w: number, h: number) {
    const vs = this.visualSpec();
    const updatedLayers = vs.layers.map(l => 
      l.id === id ? { ...l, x, y, w, h } : l
    );
    this.visualSpec.set({ ...vs, layers: updatedLayers });
  }

  updateLayerSize(id: string, x: number, y: number, w: number, h: number) {
    this.visualSpec.update(vs => ({
      ...vs,
      layers: vs.layers.map(l => 
        l.id === id ? { ...l, x: Math.round(x), y: Math.round(y), w, h } : l
      )
    }));
  }

  // Layer management
  addLayer(type: 'rect' | 'text' | 'image' | 'asset') {
    const id = `layer_${Date.now()}`;
    const newLayer: LayerSpec = {
      id,
      type,
      x: 60,
      y: 60,
      w: type === 'text' ? 400 : 200,
      h: type === 'text' ? 80 : 200
    };

    switch (type) {
      case 'rect':
        newLayer.fill = '#3a3a4e';
        newLayer.radius = 8;
        break;
      case 'text':
        newLayer.bind = '{title}';
        newLayer.fontSize = 32;
        newLayer.fontWeight = 400;
        newLayer.color = '#ffffff';
        newLayer.align = 'left';
        break;
      case 'image':
        newLayer.source = 'primaryImage';
        newLayer.fit = 'cover';
        newLayer.w = 400;
        newLayer.h = 300;
        break;
      case 'asset':
        newLayer.assetKey = 'haberbenim_logo';
        newLayer.fit = 'contain';
        newLayer.w = 100;
        newLayer.h = 100;
        break;
    }

    this.visualSpec.update(vs => ({
      ...vs,
      layers: [...vs.layers, newLayer]
    }));

    this.renderCanvas();
    this.selectLayer(id);
  }

  deleteLayer(id: string) {
    this.visualSpec.update(vs => ({
      ...vs,
      layers: vs.layers.filter(l => l.id !== id)
    }));
    
    if (this.selectedLayerId() === id) {
      this.selectLayer(null);
    }
    
    this.renderCanvas();
  }

  // Move layer UP in the list (toward top = lower array index = rendered behind)
  moveLayerUp(id: string) {
    this.visualSpec.update(vs => {
      const idx = vs.layers.findIndex(l => l.id === id);
      if (idx > 0) {
        const layers = [...vs.layers];
        [layers[idx], layers[idx - 1]] = [layers[idx - 1], layers[idx]];
        return { ...vs, layers };
      }
      return vs;
    });
    this.renderCanvas();
  }

  // Move layer DOWN in the list (toward bottom = higher array index = rendered in front)
  moveLayerDown(id: string) {
    this.visualSpec.update(vs => {
      const idx = vs.layers.findIndex(l => l.id === id);
      if (idx < vs.layers.length - 1) {
        const layers = [...vs.layers];
        [layers[idx], layers[idx + 1]] = [layers[idx + 1], layers[idx]];
        return { ...vs, layers };
      }
      return vs;
    });
    this.renderCanvas();
  }

  // Property updates
  updateLayerProperty(key: keyof LayerSpec, value: any) {
    const id = this.selectedLayerId();
    if (!id) return;

    this.visualSpec.update(vs => ({
      ...vs,
      layers: vs.layers.map(l => 
        l.id === id ? { ...l, [key]: value } : l
      )
    }));
    
    // Update shape directly without full re-render for position/size
    if (this.layer && (key === 'x' || key === 'y' || key === 'w' || key === 'h')) {
      const node = this.layer.findOne('#' + id);
      if (node) {
        if (key === 'x') node.x(value);
        else if (key === 'y') node.y(value);
        else if (key === 'w') (node as any).width(value);
        else if (key === 'h') (node as any).height(value);
        this.layer.draw();
        return;
      }
    }
    
    this.renderCanvas();
  }

  updateCanvasProperty(key: keyof VisualSpec['canvas'], value: any) {
    this.visualSpec.update(vs => ({
      ...vs,
      canvas: { ...vs.canvas, [key]: value }
    }));
    this.renderCanvas();
  }

  // Canvas size presets
  applyCanvasPreset(width: number, height: number) {
    this.visualSpec.update(vs => ({
      ...vs,
      canvas: { ...vs.canvas, w: width, h: height }
    }));
    this.renderCanvas();
  }

  isPresetActive(width: number, height: number): boolean {
    const canvas = this.visualSpec().canvas;
    return canvas.w === width && canvas.h === height;
  }

  // Variable binding
  insertVariable(varKey: string) {
    const layer = this.selectedLayer();
    if (layer && layer.type === 'text') {
      const currentBind = layer.bind || '';
      this.updateLayerProperty('bind', currentBind + `{${varKey}}`);
    }
  }

  // Gradient methods
  toggleGradient(useGradient: boolean) {
    const id = this.selectedLayerId();
    if (!id) return;

    if (useGradient) {
      this.visualSpec.update(vs => ({
        ...vs,
        layers: vs.layers.map(l => 
          l.id === id ? { 
            ...l, 
            fillGradient: {
              type: 'linear',
              angle: 180,
              colors: [l.fill || '#000000', '#ffffff'],
              stops: [0, 1]
            }
          } : l
        )
      }));
    } else {
      this.visualSpec.update(vs => ({
        ...vs,
        layers: vs.layers.map(l => {
          if (l.id === id) {
            const { fillGradient, ...rest } = l;
            return { ...rest, fill: l.fillGradient?.colors?.[0] || l.fill || '#000000' };
          }
          return l;
        })
      }));
    }
    this.renderCanvas();
  }

  updateGradientColor(index: number, color: string) {
    const id = this.selectedLayerId();
    if (!id) return;

    this.visualSpec.update(vs => ({
      ...vs,
      layers: vs.layers.map(l => {
        if (l.id === id && l.fillGradient) {
          const colors = [...l.fillGradient.colors];
          colors[index] = color;
          return { ...l, fillGradient: { ...l.fillGradient, colors } };
        }
        return l;
      })
    }));
    this.renderCanvas();
  }

  updateGradientAngle(angle: number) {
    const id = this.selectedLayerId();
    if (!id) return;

    this.visualSpec.update(vs => ({
      ...vs,
      layers: vs.layers.map(l => {
        if (l.id === id && l.fillGradient) {
          return { ...l, fillGradient: { ...l.fillGradient, angle } };
        }
        return l;
      })
    }));
    this.renderCanvas();
  }

  // Preview dialog
  openPreviewDialog(platform: string) {
    this.previewPlatform.set(platform);
    this.showPreviewDialog.set(true);
    this.generatePreview();
  }

  closePreviewDialog() {
    this.showPreviewDialog.set(false);
  }

  getResolvedCaption(key: keyof TextSpec): string {
    const template = this.textSpec()[key] || '';
    // Simple placeholder replacement for preview
    const content = this.recentContent().find(c => c.id === this.selectedContentId());
    if (!content) return template;
    
    return template
      .replace(/\{title\}/g, content.title || '')
      .replace(/\{summary\}/g, 'Özet metni...')
      .replace(/\{category\}/g, 'Gündem')
      .replace(/\{sourceName\}/g, content.sourceName || '')
      .replace(/\{url\}/g, 'https://haberbenim.com/...')
      .replace(/\{publishedAt\}/g, new Date().toLocaleDateString('tr-TR'))
      .replace(/\{hashtags\}/g, '#haber #gündem #sondakika');
  }

  // Text spec
  updateTextSpec(key: keyof TextSpec, value: string) {
    this.textSpec.update(ts => ({ ...ts, [key]: value }));
  }

  // Save
  save() {
    this.saving.set(true);
    this.saveSuccess.set(false);
    this.error.set(null);

    const request = {
      visualSpecJson: JSON.stringify(this.visualSpec()),
      textSpecJson: JSON.stringify(this.textSpec())
    };

    this.api.saveSpec(this.templateId(), request).subscribe({
      next: () => {
        this.saving.set(false);
        this.saveSuccess.set(true);
        setTimeout(() => this.saveSuccess.set(false), 3000);
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(err.error?.error || 'Failed to save');
      }
    });
  }

  // Preview
  generatePreview() {
    if (!this.selectedContentId()) {
      this.previewError.set('Lütfen bir içerik seçin');
      return;
    }

    // First save, then preview
    this.previewLoading.set(true);
    this.previewError.set(null);
    this.previewUrl.set(null);

    // Save first
    const request = {
      visualSpecJson: JSON.stringify(this.visualSpec()),
      textSpecJson: JSON.stringify(this.textSpec())
    };

    console.log('Saving spec and generating preview...');

    this.api.saveSpec(this.templateId(), request).subscribe({
      next: () => {
        console.log('Spec saved, now generating preview...');
        // Then preview
        this.api.preview(this.templateId(), {
          contentItemId: this.selectedContentId(),
          variant: 'image'
        }).subscribe({
          next: (res) => {
            this.previewLoading.set(false);
            console.log('Preview response:', res);
            // previewUrl is already a full path like /media/xxx.png
            // We need to prepend the API base
            if (res.previewUrl.startsWith('http')) {
              this.previewUrl.set(res.previewUrl);
            } else {
              // Get base URL from environment
              const baseUrl = location.origin.includes('localhost:4200') 
                ? 'http://localhost:5078' 
                : location.origin;
              this.previewUrl.set(baseUrl + res.previewUrl);
            }
          },
          error: (err) => {
            this.previewLoading.set(false);
            console.error('Preview error:', err);
            this.previewError.set(err.error?.error || 'Preview failed');
          }
        });
      },
      error: (err) => {
        this.previewLoading.set(false);
        console.error('Save error:', err);
        this.previewError.set(err.error?.error || 'Save failed');
      }
    });
  }

  goBack() {
    this.router.navigate(['/templates']);
  }

  getLayerIcon(type: string): string {
    switch (type) {
      case 'rect': return '⬛';
      case 'text': return '📝';
      case 'image': return '🖼️';
      case 'asset': return '🏷️';
      default: return '📄';
    }
  }
}

