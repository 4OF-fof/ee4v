<script setup lang="ts">
import { computed, nextTick, onMounted, ref, watch } from 'vue'
import { useData } from 'vitepress'

const props = defineProps<{
  code: string
}>()

const { isDark } = useData()
const container = ref<HTMLElement | null>(null)
const modalContainer = ref<HTMLElement | null>(null)
const error = ref('')
const scale = ref(1)
const isOpen = ref(false)
const modalSize = ref({ width: 0, height: 0 })
const isDragging = ref(false)
const source = computed(() => decodeURIComponent(props.code))
const scaleLabel = computed(() => `${Math.round(scale.value * 100)}%`)
const modalFrameStyle = computed(() => ({
  width: `${Math.max(1, modalSize.value.width * scale.value)}px`,
  height: `${Math.max(1, modalSize.value.height * scale.value)}px`
}))
let mermaidInstance: typeof import('mermaid').default | null = null
let dragStart = { x: 0, y: 0, scrollLeft: 0, scrollTop: 0 }

function clampScale(value: number) {
  return Math.min(20, Math.max(0.2, value))
}

function zoomIn() {
  scale.value = clampScale(scale.value * 1.25)
  void nextTick(applyModalScale)
}

function zoomOut() {
  scale.value = clampScale(scale.value / 1.25)
  void nextTick(applyModalScale)
}

function resetZoom() {
  scale.value = 1
  void nextTick(applyModalScale)
}

function updateModalSize() {
  const svg = modalContainer.value?.querySelector('svg')

  if (!svg) {
    return
  }

  const viewBox = svg.getAttribute('viewBox')?.split(/\s+/).map(Number)
  const rect = svg.getBoundingClientRect()
  const width = viewBox && viewBox.length === 4 && viewBox[2] > 0 ? viewBox[2] : rect.width
  const height = viewBox && viewBox.length === 4 && viewBox[3] > 0 ? viewBox[3] : rect.height

  modalSize.value = {
    width,
    height
  }

  applyModalScale()
}

function applyModalScale() {
  const svg = modalContainer.value?.querySelector('svg')

  if (!svg || modalSize.value.width <= 0 || modalSize.value.height <= 0) {
    return
  }

  const width = modalSize.value.width * scale.value
  const height = modalSize.value.height * scale.value

  svg.setAttribute('width', `${width}`)
  svg.setAttribute('height', `${height}`)
  svg.style.maxWidth = 'none'
  svg.style.width = `${width}px`
  svg.style.height = `${height}px`
}

function fitToWidth() {
  const viewport = modalContainer.value?.closest('.mermaid-dialog__viewport') as HTMLElement | null
  const viewportWidth = viewport?.clientWidth ?? 0

  if (viewportWidth <= 0 || modalSize.value.width <= 0) {
    return
  }

  scale.value = clampScale((viewportWidth - 24) / modalSize.value.width)
  void nextTick(applyModalScale)
}

function zoomAtPoint(event: WheelEvent) {
  const viewport = event.currentTarget as HTMLElement
  const previousScale = scale.value
  const nextScale = clampScale(previousScale * (event.deltaY < 0 ? 1.15 : 1 / 1.15))

  if (nextScale === previousScale) {
    return
  }

  event.preventDefault()

  const rect = viewport.getBoundingClientRect()
  const pointerX = event.clientX - rect.left
  const pointerY = event.clientY - rect.top
  const contentX = (viewport.scrollLeft + pointerX) / previousScale
  const contentY = (viewport.scrollTop + pointerY) / previousScale

  scale.value = nextScale

  void nextTick(() => {
    applyModalScale()
    viewport.scrollLeft = contentX * nextScale - pointerX
    viewport.scrollTop = contentY * nextScale - pointerY
  })
}

function startDrag(event: PointerEvent) {
  const viewport = event.currentTarget as HTMLElement

  if (event.button !== 0) {
    return
  }

  isDragging.value = true
  dragStart = {
    x: event.clientX,
    y: event.clientY,
    scrollLeft: viewport.scrollLeft,
    scrollTop: viewport.scrollTop
  }
  viewport.setPointerCapture(event.pointerId)
}

function dragViewport(event: PointerEvent) {
  if (!isDragging.value) {
    return
  }

  const viewport = event.currentTarget as HTMLElement
  viewport.scrollLeft = dragStart.scrollLeft - (event.clientX - dragStart.x)
  viewport.scrollTop = dragStart.scrollTop - (event.clientY - dragStart.y)
}

function stopDrag(event: PointerEvent) {
  const viewport = event.currentTarget as HTMLElement
  isDragging.value = false

  if (viewport.hasPointerCapture(event.pointerId)) {
    viewport.releasePointerCapture(event.pointerId)
  }
}

function openModal() {
  isOpen.value = true
  scale.value = 1

  void nextTick(async () => {
    await renderInto(modalContainer.value)
    updateModalSize()
    fitToWidth()
  })
}

function closeModal() {
  isOpen.value = false
}

async function getMermaid() {
  if (!mermaidInstance) {
    const module = await import('mermaid')
    mermaidInstance = module.default
  }

  mermaidInstance.initialize({
    startOnLoad: false,
    securityLevel: 'loose',
    theme: isDark.value ? 'dark' : 'default'
  })

  return mermaidInstance
}

async function renderInto(target: HTMLElement | null) {
  if (!target) {
    return
  }

  const mermaid = await getMermaid()
  target.removeAttribute('data-processed')
  target.textContent = source.value

  await mermaid.run({
    nodes: [target]
  })
}

async function renderDiagram() {
  error.value = ''

  try {
    await nextTick()
    await renderInto(container.value)

    if (isOpen.value) {
      await renderInto(modalContainer.value)
      updateModalSize()
    }
  } catch (err) {
    error.value = err instanceof Error ? err.message : String(err)
  }
}

onMounted(renderDiagram)
watch([source, isDark], renderDiagram)
</script>

<template>
  <div class="mermaid-diagram">
    <div v-if="error" class="mermaid-diagram__error">{{ error }}</div>
    <div v-show="!error" class="mermaid-diagram__preview">
      <button type="button" class="mermaid-diagram__open" title="Open diagram viewer" @click="openModal">
        <span>Open</span>
      </button>
      <div ref="container" class="mermaid"></div>
    </div>
    <Teleport to="body">
      <div v-if="isOpen" class="mermaid-dialog" role="dialog" aria-modal="true" @click.self="closeModal">
        <div class="mermaid-dialog__panel">
          <div class="mermaid-dialog__toolbar" aria-label="Diagram zoom controls">
            <button type="button" class="mermaid-diagram__button" title="Zoom out" @click="zoomOut">-</button>
            <button type="button" class="mermaid-diagram__button" title="Reset zoom" @click="resetZoom">
              {{ scaleLabel }}
            </button>
            <button type="button" class="mermaid-diagram__button" title="Zoom in" @click="zoomIn">+</button>
            <button
              type="button"
              class="mermaid-diagram__button"
              title="Fit to width"
              @click="fitToWidth"
            >
              Fit
            </button>
            <button type="button" class="mermaid-diagram__button" title="Close" @click="closeModal">
              Close
            </button>
          </div>
          <div
            class="mermaid-dialog__viewport"
            :class="{ 'is-dragging': isDragging }"
            @wheel="zoomAtPoint"
            @pointerdown="startDrag"
            @pointermove="dragViewport"
            @pointerup="stopDrag"
            @pointerleave="stopDrag"
          >
            <div class="mermaid-dialog__frame" :style="modalFrameStyle">
              <div ref="modalContainer" class="mermaid mermaid-dialog__canvas"></div>
            </div>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>
